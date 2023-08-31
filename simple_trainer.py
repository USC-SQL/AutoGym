import gymnasium as gym
from gymnasium.wrappers import TimeLimit
from gymnasium.vector import SyncVectorEnv, AsyncVectorEnv
from torch.utils.tensorboard import SummaryWriter
from tensorboard.backend.event_processing import event_accumulator
from trainer_common import RollingMean, InfoProcessor, make_env
import json
import random
import numpy as np
import os.path
import sys

ACTION_SELECTION_MODE_NULL = 1
ACTION_SELECTION_MODE_RANDOM = 2

def run_simple(game_config, game_config_path, start_port, workdir, is_predict, is_deterministic, action_sel_mode):
    assert action_sel_mode == ACTION_SELECTION_MODE_NULL or action_sel_mode == ACTION_SELECTION_MODE_RANDOM
    if is_predict:
        if is_deterministic:
            random.seed(1234)
        num_envs = 1
        env_fns = [make_env(game_config, game_config_path, workdir, start_port, None, False)]
        env = SyncVectorEnv(env_fns)
    else:
        pre_init = game_config['env_config']['pre_init']
        num_envs = game_config['num_envs']
        env_fns = [make_env(game_config, game_config_path, workdir, start_port + 2*i, start_port + 2*i + 1 if pre_init else None, True) for i in range(num_envs)]
        env = AsyncVectorEnv(env_fns, shared_memory=False, daemon=False)
    if not is_predict:
        tboard_log_path = game_config['tensorboard_log_path']
        tboard_log_dir = os.path.dirname(tboard_log_path)
        if not os.path.exists(tboard_log_dir):
            print('Creating directory: {}'.format(tboard_log_dir))
            os.makedirs(tboard_log_dir)
        log_path = os.path.join(tboard_log_path, game_config['tensorboard_log_name'])
        writer = SummaryWriter(log_path)
        ep_rew_mean = RollingMean()
        ea = event_accumulator.EventAccumulator(writer.log_dir)
        ea.Reload()
        if 'episode/reward' in ea.scalars.Keys():
            for evt in ea.scalars.Items('episode/reward')[-ep_rew_mean.buf.maxlen:]:
                ep_rew_mean.add_value(evt.value)
    step_num = 0
    info_proc = InfoProcessor(game_config, workdir)
    checkpoint_path = game_config['trainer_config']['checkpoint_path']
    checkpoint_dir = os.path.dirname(checkpoint_path)
    try:
        if not os.path.exists(checkpoint_dir):
            print('Creating directory: {}'.format(checkpoint_dir))
            os.makedirs(checkpoint_dir)
        if not is_predict:
            if os.path.exists(checkpoint_path):
                with open(checkpoint_path, 'r') as f:
                    chkpt = json.loads(f.read())
                    step_num = chkpt['step_num']
        def save_checkpoint():
            save_data = dict(
                step_num=step_num)
            with open(checkpoint_path, 'w') as f:
                f.write(json.dumps(save_data))
        if not is_predict:
            max_steps = game_config['num_steps']
        else:
            max_steps = game_config['env_config']['time_limit']
        observation, info = env.reset()
        dones = np.array([False for _ in range(num_envs)])
        ep_rews = np.array([0.0 for _ in range(num_envs)])
        while step_num < max_steps:
            if np.all(dones):
                if is_predict:
                    break
                else:
                    observation, info = env.reset()
            if action_sel_mode == ACTION_SELECTION_MODE_RANDOM:
                action_masks = observation['action_mask']
                valid_actions = [
                    [action for action in range(len(action_mask)) if action_mask[action]]
                    for action_mask in action_masks
                ]
                print("Valid action counts: {}".format([len(env_valid_actions) for env_valid_actions in valid_actions]))
                actions = np.array([random.sample(env_valid_actions, 1)[0] for env_valid_actions in valid_actions])
            else: # ACTION_SELECTION_MODE_NULL
                actions = np.array([0 for _ in range(num_envs)])
            print("Performing actions: {}".format(actions))
            observation, rewards, terms, truncs, info = env.step(actions)
            dones = np.logical_or(terms, truncs)
            ep_rews += rewards
            if not is_predict:
                if np.any(dones):
                    for i in range(num_envs):
                        if dones[i]:
                            ep_rew_mean.add_value(ep_rews[i])
                            writer.add_scalar('episode/reward', ep_rews[i], global_step=step_num)
                            ep_rews[i] = 0.0
                    writer.add_scalar('episode/reward_mean', ep_rew_mean.get_mean(), global_step=step_num)
                    writer.flush()
                    save_checkpoint()
                info_proc.process_info(info, step_num)
                info_proc.process_observation(observation, step_num)
            print('Info: {}'.format(info))
            print("Rewards: {}".format(rewards))
            sys.stdout.flush()
            step_num += num_envs
    finally:
        env.close()
        info_proc.close()
