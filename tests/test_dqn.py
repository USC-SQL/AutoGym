import gymnasium as gym
import gymnasium.spaces as spaces
from tensorboard.backend.event_processing import event_accumulator
from gymnasium import ObservationWrapper
import torch
import torch.nn as nn
import numpy as np
import math
import unittest
import trainer_common
from unittest.mock import patch
from collections import deque
import os.path
import shutil
import dqn_trainer

class DQNSimpleNet(nn.Module):
    def __init__(self, observation_space, num_actions):
        super().__init__()
        in_features = observation_space.shape[0]
        assert len(observation_space.shape) == 1
        self.linear = nn.Sequential(
            nn.Linear(in_features, 128),
            nn.ReLU(),
            nn.Linear(128, 128),
            nn.ReLU(),
            nn.Linear(128, num_actions))

    def forward(self, obs):
        logits = self.linear(obs)
        return logits

class CartpoleSimpleWrapper(ObservationWrapper):
    def __init__(self, env):
        super().__init__(env)
        self.observation_space = gym.spaces.Dict({
            'obs': env.observation_space,
            'action_mask': spaces.Box(low=0, high=1, shape=(env.action_space.n,), dtype=np.float32)
        })

    def observation(self, obs):
        return {
            'obs': obs,
            'action_mask': np.array([1.0 for _ in range(self.action_space.n)])
        }


class StackWrapper(ObservationWrapper):
    def __init__(self, env):
        super().__init__(env)
        self._stack_len = 4
        self._buf = deque(maxlen=self._stack_len)
        shape = env.observation_space.shape
        self.observation_space = spaces.Box(low=0.0, high=1.0,
                                            shape=(self._stack_len, shape[0], shape[1]),
                                            dtype=env.observation_space.dtype)

    def observation(self, obs):
        self._buf.append(obs)
        channels = []
        for x in self._buf:
            channels.append(x)
        while len(channels) < self._stack_len:
            channels.append(self._buf[-1])
        return np.stack(channels)


def make_env_cartpole_simple(game_config, game_config_path, workdir, port, pre_init_port, is_training):
    def _init():
        return CartpoleSimpleWrapper(gym.make('CartPole-v1', render_mode=None))
    return _init


class DQNSimpleTestCase(unittest.TestCase):
    game_config = {
        'config_name': 'cartpole_simple_test',
        'game_exe': '',
        'trainer': 'dqn',
        'env_config': {
            'num_observation_features': 0,
            'observation_includes_image': True,
            'pre_init': False,
            'time_limit': 300
        },
        'num_envs': 16,
        'num_steps': 375000,
        'tensorboard_log_path': 'test_results/tensorboard_logs/cartpole',
        'tensorboard_log_name': 'cartpole_simple',
        'info_db_path': 'test_results/info/cartpole_simple.db',
        'trainer_config': {
            'env_reward': True,
            "learning_rate": 0.0001,
            "discount_factor": 0.99,
            "buffer_size": 5000,
            "batch_size": 64,
            "target_update_freq": 500,
            "eps_initial": 1.0,
            "eps_final": 0.05,
            "eps_annealing_duration": 0.2,
            "checkpoint_path": "test_results/checkpoints/cartpole_simple"
        }
    }

    def setUp(self):
        if os.path.exists('test_results/tensorboard_logs/cartpole/cartpole_simple'):
            shutil.rmtree('test_results/tensorboard_logs/cartpole/cartpole_simple')
        if os.path.exists('test_results/checkpoints/cartpole_simple.pth'):
            os.remove('test_results/checkpoints/cartpole_simple.pth')
        if os.path.exists('test_results/info/cartpole_simple.db'):
            shutil.rmtree('test_results/info/cartpole_simple.db')

    @patch('dqn_trainer.InfoProcessor')
    @patch('dqn_trainer.make_env', new=make_env_cartpole_simple)
    @patch('dqn_trainer.DQNNet', new=DQNSimpleNet)
    def test_dqn_simple(self, mock_info_proc):
        game_config = DQNSimpleTestCase.game_config
        dqn_trainer.run_dqn(game_config, '', 0, 'test_results/workdir', False, False)

        # check that training achieved expected results
        log_dir = os.path.join(game_config['tensorboard_log_path'], game_config['tensorboard_log_name'])
        ea = event_accumulator.EventAccumulator(log_dir)
        ea.Reload()
        num_max = 0
        for evt in ea.scalars.Items('episode/reward'):
            if abs(evt.value - 500) < 0.01:
                num_max += 1
        self.assertGreaterEqual(num_max, 10)
        target_mean_reached = False
        for evt in ea.scalars.Items('episode/reward_mean'):
            if evt.value >= 350:
                target_mean_reached = True
                break
        self.assertTrue(target_mean_reached)

        # check that restoring from checkpoints works
        ep_rews = []
        def ep_rew_hook(self, rew):
            ep_rews.append(rew)
        with patch('dqn_trainer.RollingMean.add_value', new=ep_rew_hook) as _:
            dqn_trainer.run_dqn(game_config, '', 0, 'test_results/workdir', True, False)
            ep_rews = np.array(ep_rews)
            self.assertGreaterEqual(np.mean(ep_rews), 300)
