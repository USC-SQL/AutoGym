import gymnasium as gym
from gymnasium import spaces
from gymnasium.envs.registration import register
from collections import deque
import os.path
import subprocess
import socket
import psutil
import struct
import json
import uuid
import numpy as np
import time
import traceback
import numpy as mp
import skimage

class UnityGameInstanceException(Exception):
    def __init__(self, message):
        super().__init__(message)

class UnityGameInstance:
    def __init__(self, identifier, game_exe, host_addr, port, game_config_path, work_dir_base, training_mode, blocking=True):
        self._identifier = identifier
        self._game_exe = game_exe
        self._host_addr = host_addr
        self._port = port
        self._game_config_path = game_config_path
        self._work_dir = os.path.join(work_dir_base, str(uuid.uuid4()))
        self._training_mode = training_mode
        self._process = None
        self._socket = None
        self._blocking = blocking
        self._recvbuf = b''
        self._msglen = None
        self._connected = False
        self._init_msg = None

    def _check_port_open(self, port):
        attempts = 0
        max_attempts = 5
        while attempts < max_attempts:
            conn_using = [conn for conn in psutil.net_connections() if conn.status != 'TIME_WAIT' and conn.laddr.port == port]
            if len(conn_using) == 0:
                return True
            conn = conn_using[0]
            proc = psutil.Process(conn.pid)
            if proc.name() == os.path.basename(self._game_exe):
                proc.kill()
                time.sleep(5.0)
            else:
                break
            attempts += 1
        raise Exception('port already in use')

    def _receive_bytes(self, n):
        while len(self._recvbuf) < n:
            remaining = n - len(self._recvbuf)
            if self._blocking:
                data = self._socket.recv(remaining)
                self._recvbuf += data
            else:
                for _ in range(remaining):
                    try:
                        data = self._socket.recv(1)
                        self._recvbuf += data
                    except BlockingIOError:
                        break
                break
        assert not len(self._recvbuf) > n
        if len(self._recvbuf) == n:
            data = self._recvbuf
            self._recvbuf = b''
            return data
        else:
            return None

    def _receive_message(self):
        if self._msglen is None:
            blen = self._receive_bytes(4)
            if blen is not None:
                self._msglen = struct.unpack('i', blen)[0]
            else:
                if self._blocking:
                    raise UnityGameInstanceException('IO failure: failed to receive message length')
                else:
                    return None
        if self._msglen is not None:
            bmsg = self._receive_bytes(self._msglen)
            if bmsg is not None:
                self._msglen = None
                return json.loads(bmsg.decode('utf-8'))
            else:
                if self._blocking:
                    raise UnityGameInstanceException('IO failure: failed to receive message')
                else:
                    return None

    def _send_message(self, msg):
        s = json.dumps(msg)
        bmsg = s.encode('utf-8')
        blen = struct.pack('i', len(bmsg))
        self._socket.sendall(blen)
        self._socket.sendall(bmsg)

    def set_blocking(self, blocking):
        if self._blocking != blocking:
            self._blocking = blocking
            if self._socket is not None:
                self._socket.setblocking(blocking)
                if blocking:
                    self._socket.settimeout(60)

    def _init_socket(self):
        if self._socket is not None:
            self._socket.close()
            self._socket = None
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        if self._blocking:
            self._socket.settimeout(60)
        else:
            self._socket.setblocking(False)

    def start(self):
        self._check_port_open(self._port)
        os.makedirs(self._work_dir)
        env = dict()
        env.update(os.environ)
        env['HOME'] = self._work_dir
        env['RLENV_ID'] = self._identifier
        env['RLENV_ADDR'] = self._host_addr
        env['RLENV_PORT'] = str(self._port)
        env['RLENV_CONFIG'] = self._game_config_path
        env['RLENV_WORKDIR'] = self._work_dir
        env['RLENV_TRAINING_MODE'] = "true" if self._training_mode else "false"
        self._process = subprocess.Popen([self._game_exe], env=env, cwd=os.path.dirname(self._game_exe))
        self._init_socket()

    def connect(self):
        assert self.is_started()
        if self._connected:
            print('Warning: called connect() on game instance that is already connected')
            return True
        if self._blocking:
            attempts = 0
            max_attempts = 10
            while attempts < max_attempts:
                try:
                    try:
                        self._socket.connect((self._host_addr, self._port))
                    except OSError as e:
                        if e.errno == 10056 or e.errno == 106: # already connected
                            pass
                        else:
                            raise
                    attempts = 0
                    self._connected = True
                    return True
                except (ConnectionRefusedError, TimeoutError):
                    attempts += 1
                    self._init_socket()
                    time.sleep(1.0)
            raise UnityGameInstanceException(
                'failed to connect to game instance within {} attempts'.format(max_attempts))
        else:
            res = self._socket.connect_ex((self._host_addr, self._port))
            if res == 10056 or res == 106: # already connected (windows is 10056, linux is 106)
                self._connected = True
                return True
            else:
                return False

    def initialize(self):
        assert self.is_started() and self.is_connected()
        if self.is_initialized():
            print('Warning: called initialize() on already initialized game instance')
            return True
        else:
            if self._blocking:
                while True:
                    msg = self._receive_message()
                    if msg['ready']:
                        self._init_msg = msg
                        return True
            else:
                msg = self._receive_message()
                if msg is None:
                    return False
                if msg['ready']:
                    self._init_msg = msg
                    return True
                else:
                    return False

    def send_action(self, action):
        self._send_message({'action': int(action)})

    def send_wait(self):
        self._send_message({'wait': True})

    def receive_state(self):
        assert self.is_connected() and self.is_initialized()
        return self._receive_message()

    def get_port(self):
        return self._port

    def is_started(self):
        return self._process is not None

    def is_connected(self):
        return self._connected

    def is_initialized(self):
        return self._init_msg is not None

    def get_init_message(self):
        return self._init_msg

    def close(self):
        if self._socket is not None:
            self._socket.close()
        if self._process is not None:
            self._process.kill()
            try:
                self._process.wait(timeout=60)
            except subprocess.TimeoutExpired:
                raise Exception('failed to terminate game process')

class UnityEnv(gym.Env):
    metadata = {'render_modes': ['human']}

    def __init__(self, identifier, game_exe, env_config, game_config_path, work_dir, port, host_addr='127.0.0.1', pre_init_port=None, training_mode=False):
        required_params = {'identifier', 'game_exe', 'game_config_path', 'work_dir', 'port'}
        for param in required_params:
            if not locals()[param]:
                raise Exception('missing required environment parameter \'{}\''.format(param))
        self._is_image_obs = env_config['observation_includes_image']
        if self._is_image_obs:
            [w, h] = env_config['image_resize_to']
            self._resize_image_to = (h, w)
            num_obs_channels = env_config['observation_stack'] if 'observation_stack' in env_config else 1
            obs_space = spaces.Box(low=0, high=255, shape=(num_obs_channels, h, w), dtype=np.uint8)
        else:
            obs_space = spaces.Box(low=-np.inf, high=np.inf,
                                   shape=(env_config['num_observation_features'],), dtype=np.float32)
            if 'observation_stack' in env_config:
                raise Exception('observation_stack with vector observations is not supported')
        self.action_space = spaces.Discrete(env_config['num_actions'])
        self.observation_space = spaces.Dict({
            'obs': obs_space,
            'action_mask': spaces.Box(low=0, high=1, shape=(self.action_space.n,), dtype=np.float32)
        })
        self._identifier = identifier
        self._game_exe = game_exe
        self._host_addr = host_addr
        self._game_port = port
        self._game_config_path = game_config_path
        self._work_dir = work_dir
        self._training_mode = training_mode
        self._game_inst = None
        self._pre_init = pre_init_port is not None
        if self._pre_init:
            self._pre_init_port = pre_init_port
            self._pre_init_inst = None
        self._action_mask = np.array([1.0] + [0.0]*(self.action_space.n-1), dtype=np.float32)
        self._obs_buffer = deque(maxlen=env_config['observation_stack'] if 'observation_stack' in env_config else 1)

    def _update_action_mask(self, invalid_actions):
        invalid_action_set = set(invalid_actions)
        for action in range(0, self.action_space.n):
            self._action_mask[action] = 0.0 if action in invalid_action_set else 1.0

    def _read_observation(self, observation):
        if self._is_image_obs:
            img_path = observation['img']
            img_data = skimage.io.imread(img_path)
            img_data = skimage.color.rgb2gray(
                skimage.transform.resize(img_data[:,:,:3], self._resize_image_to,
                                                anti_aliasing=False, preserve_range=True)).astype(np.uint8)
            self._obs_buffer.append(img_data)
            layers = list(self._obs_buffer)
            while len(layers) < self._obs_buffer.maxlen:
                layers.append(layers[-1])
            img_data = np.stack(layers)
            os.remove(img_path)
            return img_data
        else:
            return np.array(observation, dtype=np.float32)

    def _read_info(self, info):
        return info

    def reset(self, seed=None, options=None):
        if self._game_inst is not None:
            self._game_inst.close()
            self._game_inst = None
        if self._pre_init and self._pre_init_inst is not None:
            prev_port = self._game_port
            self._game_port = self._pre_init_port
            self._pre_init_port = prev_port
            self._game_inst = self._pre_init_inst
            self._pre_init_inst = None
            self._game_inst.set_blocking(True)
        else:
            self._game_inst = UnityGameInstance(self._identifier, self._game_exe, self._host_addr, self._game_port, self._game_config_path, self._work_dir, self._training_mode, blocking=True)
        if self._pre_init:
            self._pre_init_inst = UnityGameInstance(self._identifier, self._game_exe, self._host_addr, self._pre_init_port, self._game_config_path, self._work_dir, self._training_mode, blocking=False)
            self._pre_init_inst.start()
        if not self._game_inst.is_started():
            self._game_inst.start()
        if not self._game_inst.is_connected():
            time.sleep(1.0)
            self._game_inst.connect()
        if not self._game_inst.is_initialized():
            self._game_inst.initialize()
        if self._pre_init and self._pre_init_inst.connect(): # try connecting/initializing once to background copy
            self._pre_init_inst.initialize()
        msg = self._game_inst.get_init_message()
        if msg['numActions'] != self.action_space.n:
            raise Exception('action space size in configuration ({}) does not match game client ({})'.format(self.action_space.n, msg['numActions']))
        self._update_action_mask(msg['invalidActions'])
        obs = self._read_observation(msg['observation'])
        info = self._read_info(msg['info'])
        observation = {'obs': obs, 'action_mask': self._action_mask}
        return observation, info

    def step(self, action):
        if self._pre_init:
            if not self._pre_init_inst.is_connected():
                self._pre_init_inst.connect()
            elif not self._pre_init_inst.is_initialized():
                self._pre_init_inst.initialize()
            else:
                self._pre_init_inst.send_wait()
        self._game_inst.send_action(action)
        msg = self._game_inst.receive_state()
        reward = msg['reward']
        done = msg['done']
        if not done:
            obs = self._read_observation(msg['observation'])
            info = self._read_info(msg['info'])
            self._update_action_mask(msg['invalidActions'])
        else:
            obs = np.zeros(self.observation_space['obs'].shape, self.observation_space['obs'].dtype)
            info = dict()
        observation = {'obs': obs, 'action_mask': self._action_mask}
        return observation, reward, done, False, info

    def close(self):
        if self._game_inst is not None:
            self._game_inst.close()
            self._game_inst = None
        if self._pre_init and self._pre_init_inst is not None:
            self._pre_init_inst.close()
            self._pre_init_inst = None

register(
    id='USC-SQL/UnityEnv-v0',
    entry_point='unity_env:UnityEnv'
)
