import gymnasium as gym
from gymnasium.vector import SyncVectorEnv, AsyncVectorEnv
import numpy as np
import torch
from tensorboard.backend.event_processing import event_accumulator
from torch.utils.tensorboard import SummaryWriter
from torch import nn
import torch.optim as optim
from cpprb import ReplayBuffer
import os.path
import math
import sys
from trainer_common import RollingMean, InfoProcessor, make_env

class DQNNet(nn.Module):
    def __init__(self, observation_space, num_actions):
        super().__init__()
        n_in_channels = observation_space.shape[0]
        self.cnn = nn.Sequential(
            nn.Conv2d(in_channels=n_in_channels, out_channels=32, kernel_size=8, stride=4),
            nn.ReLU(),
            nn.Conv2d(in_channels=32, out_channels=64, kernel_size=4, stride=2),
            nn.ReLU(),
            nn.Conv2d(in_channels=64, out_channels=64, kernel_size=3, stride=1),
            nn.ReLU(),
            nn.Flatten())

        with torch.no_grad():
            n_features = self.cnn(torch.as_tensor(observation_space.sample()[np.newaxis], dtype=torch.float)).shape[1]

        self.linear = nn.Sequential(
            nn.Linear(n_features, 512),
            nn.ReLU(),
            nn.Linear(512, num_actions))

    def forward(self, obs):
        assert obs.dtype == torch.uint8
        obs_norm = obs.float()/255.0
        logits = self.linear(self.cnn(obs_norm))
        return logits

def run_dqn(game_config, game_config_path, start_port, workdir, is_predict, is_deterministic):
    if is_predict:
        num_envs = 1
    else:
        num_envs = game_config['num_envs']
    if not game_config['env_config']['observation_includes_image'] or \
            game_config['env_config']['num_observation_features'] > 0:
        raise NotImplementedError()
    log_dir = os.path.join(game_config['tensorboard_log_path'],
                            game_config['tensorboard_log_name'])
    if not is_predict:
        if not os.path.exists(log_dir):
            print('Creating directory: {}'.format(log_dir))
            os.makedirs(log_dir)
        writer = SummaryWriter(log_dir=log_dir)
    dummy_env = make_env(game_config, game_config_path, workdir, start_port, None, True)()
    observation_space = dummy_env.observation_space
    obs_space = observation_space['obs']
    num_actions = dummy_env.action_space.n
    dummy_env.close()
    checkpoint_path = game_config['trainer_config']['checkpoint_path'] + '.pth'
    pre_init = game_config['env_config']['pre_init']
    env_fns = [make_env(game_config, game_config_path, workdir,
                        start_port + i*2, start_port + i*2 + 1 if pre_init and not is_predict else None, not is_predict)
               for i in range(num_envs)]
    env = AsyncVectorEnv(env_fns, shared_memory=False, daemon=False)
    info_proc = InfoProcessor(game_config, workdir)
    try:
        rb = ReplayBuffer(game_config['trainer_config']['buffer_size'], env_dict={
            'obs': {'shape': obs_space.shape, 'dtype': obs_space.dtype},
            'act': {'dtype': np.int_},
            'rew': {'dtype': np.float32},
            'next_obs': {'shape': obs_space.shape, 'dtype': obs_space.dtype},
            'next_act_mask': {'shape': (num_actions,), 'dtype': observation_space['action_mask'].dtype},
            'done': {'dtype': np.bool_}
        })
        device = torch.device('cpu')
        print('Using device: {}'.format(device))

        use_count_reward = 'count_reward' in game_config['trainer_config'] and game_config['trainer_config']['count_reward']
        use_env_reward = 'env_reward' in game_config['trainer_config'] and game_config['trainer_config']['env_reward']

        predictor = DQNNet(obs_space, num_actions).to(device)
        target = DQNNet(obs_space, num_actions).to(device)

        if use_count_reward:
            state_visit_counts = [dict() for _ in range(num_envs)]
            print('Using count-based exploration reward')
        if use_env_reward:
            print('Using environment reward')

        init_step_num = 0
        if os.path.exists(checkpoint_path):
            print('Loading existing checkpoint: {}'.format(checkpoint_path))
            data = torch.load(checkpoint_path)
            predictor.load_state_dict(data['state_dict'])
            if not is_predict:
                init_step_num = data['step_num']
        checkpoint_dir = os.path.dirname(checkpoint_path)
        if not os.path.exists(checkpoint_dir):
            print('Creating directory: {}'.format(checkpoint_dir))
            os.makedirs(checkpoint_dir)

        target.load_state_dict(predictor.state_dict())
        optimizer = optim.Adam(predictor.parameters(), lr=game_config['trainer_config']['learning_rate'])
        loss_fn = nn.MSELoss()

        predictor.train()
        target.eval()

        ep_rew_mean = RollingMean()
        ea = event_accumulator.EventAccumulator(log_dir)
        ea.Reload()
        if 'episode/reward' in ea.scalars.Keys():
            for evt in ea.scalars.Items('episode/reward')[-ep_rew_mean.buf.maxlen:]:
                ep_rew_mean.add_value(evt.value)

        if is_predict:
            max_step_num = game_config['env_config']['time_limit']-1
        else:
            max_step_num = game_config['num_steps']

        ep_rews = np.zeros((num_envs,))
        eps_initial = game_config['trainer_config']['eps_initial']
        eps_final = game_config['trainer_config']['eps_final']
        eps_anneal_steps = max_step_num*game_config['trainer_config']['eps_annealing_duration']
        gamma = game_config['trainer_config']['discount_factor']
        target_update_freq = game_config['trainer_config']['target_update_freq']
        batch_size = game_config['trainer_config']['batch_size']
        log_freq = 500
        save_freq = 1500

        sys.stdout.flush()

        step_num = init_step_num
        last_log = step_num
        last_target_update = step_num
        last_save = step_num
        observation, info = env.reset()
        while step_num <= max_step_num:
            if is_predict:
                if is_deterministic:
                    eps = 0.0
                else:
                    eps = eps_final
            else:
                if step_num <= eps_anneal_steps:
                    eps = eps_initial + step_num/eps_anneal_steps*(eps_final - eps_initial)
                else:
                    eps = eps_final

            if step_num - last_log >= log_freq:
                msg = 'Step: {}/{}, Eps: {}'.format(step_num, max_step_num, eps)
                if len(ep_rew_mean.buf) > 0:
                    msg += ", Mean Reward: {}".format(ep_rew_mean.get_mean())
                print(msg)
                sys.stdout.flush()
                if not is_predict:
                    writer.add_scalar('train/epsilon', eps, step_num)
                last_log = step_num

            # gain experience
            with torch.no_grad():
                obs = observation['obs']
                if torch.rand(1)[0] < eps:
                    action_values = torch.randn((num_envs, num_actions), device=device)
                else:
                    action_values = predictor(torch.as_tensor(obs, device=device))
                action_mask = torch.as_tensor(observation['action_mask'], device=device)
                print('Valid action counts: {}'.format([int(env_action_mask.sum()) for env_action_mask in action_mask]))
                min_value = action_values.min() - action_values.max() - 1.0
                action_values = action_values + (1.0 - action_mask)*min_value
                actions = action_values.argmax(1).cpu()
                print('Performing actions: {}'.format(actions))
                observation, rew, term, trunc, info = env.step(actions.numpy())
                if not use_env_reward:
                    rew = np.zeros(rew.shape)

            if use_count_reward:
                for i in range(num_envs):
                    env_state_hash = info['state_hash'][i]
                    env_visit_counts = state_visit_counts[i]
                    if env_state_hash in env_visit_counts:
                        visit_count = env_visit_counts[env_state_hash]
                    else:
                        visit_count = 0
                    visit_count += 1
                    rew[i] += 1.0/math.sqrt(visit_count)
                    env_visit_counts[env_state_hash] = visit_count

            print('Rewards: {}'.format(rew))

            ep_rews += rew
            step_num += num_envs
            dones = np.logical_or(term, trunc)
            next_obs = observation['obs']
            next_act_mask = observation['action_mask']
            for i in range(num_envs):
                if dones[i]:
                    ep_rew_mean.add_value(ep_rews[i])
                    if not is_predict:
                        writer.add_scalar('episode/reward', ep_rews[i], step_num)
                    ep_rews[i] = 0
                    if use_count_reward:
                        state_visit_counts[i].clear()
                rb.add(obs=obs[i], act=actions[i], next_obs=next_obs[i],
                       next_act_mask=next_act_mask[i], rew=rew[i], done=dones[i])
            if not is_predict:
                if np.any(dones):
                    writer.add_scalar('episode/reward_mean', ep_rew_mean.get_mean(), step_num)
                    writer.flush()
                info_proc.process_info(info, step_num)
                info_proc.process_observation(observation, step_num)

            # train
            if not is_predict and rb.get_stored_size() >= batch_size:
                sample = rb.sample(batch_size)
                done_mask = torch.where(torch.as_tensor(sample['done'], device=device).squeeze(),
                                        torch.zeros(batch_size, device=device),
                                        torch.ones(batch_size, device=device))
                tgt_action_values = target(torch.as_tensor(sample['next_obs'], device=device))
                min_value = tgt_action_values.min() - tgt_action_values.max() - 1.0
                next_act_mask = torch.as_tensor(sample['next_act_mask'], device=device)
                tgt_action_values = tgt_action_values + (1.0 - next_act_mask)*min_value
                expected = torch.as_tensor(sample['rew'].squeeze(), device=device) + \
                           gamma * done_mask * tgt_action_values.max(1).values
                current = torch.gather(
                    predictor(torch.as_tensor(sample['obs'], device=device)),
                    1, torch.as_tensor(sample['act'], device=device, dtype=torch.int64)).squeeze()
                loss = loss_fn(current, expected)
                optimizer.zero_grad()
                loss.backward()
                optimizer.step()
                if step_num - last_target_update >= target_update_freq:
                    target.load_state_dict(predictor.state_dict())
                    last_target_update = step_num
                if step_num - last_save >= save_freq:
                    save_data = {
                        'state_dict': predictor.state_dict(),
                        'step_num': step_num
                    }
                    torch.save(save_data, checkpoint_path)
                    last_save = step_num
    finally:
        env.close()
        info_proc.close()
