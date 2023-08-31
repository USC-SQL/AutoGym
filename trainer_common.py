import os.path
import sqlite3
from collections import deque
import os.path
import glob
import shutil
import subprocess
import json
import random
import numpy as np
import unity_env
import gymnasium as gym
from gymnasium.wrappers import TimeLimit


class RollingMean:
    def __init__(self, buf_size=100):
        self.buf = deque(maxlen=buf_size)

    def add_value(self, value):
        self.buf.extend([value])

    def get_mean(self):
        return sum(self.buf)/len(self.buf)


class InfoProcessor:
    def __init__(self, game_config, workdir):
        self._config_name = game_config['config_name']
        self._coverage_json_path = os.path.join(os.path.dirname(game_config['game_exe']), 'coverage.json')
        self._managed_dir = glob.glob(os.path.join(os.path.dirname(game_config['game_exe']), '*_Data', 'Managed'))[0]
        self._altcover_cmd = os.getenv('RLEXP_ALTCOVER_CMD') or 'altcover'
        info_db_dir = os.path.dirname(game_config['info_db_path'])
        if not os.path.exists(info_db_dir):
            print('Creating directory: {}'.format(info_db_dir))
            os.makedirs(info_db_dir)
        self._db_conn = sqlite3.connect(game_config['info_db_path'])
        self._init_db()
        self._info_workdir = os.path.join(workdir, 'InfoProcessing_{}'.format(os.getpid()))
        if os.path.exists(self._info_workdir):
            self._remove_workdir()
        self._obs_dump_dir = os.path.join(workdir, 'ObservationDumps_{}'.format(os.getpid()))
        os.makedirs(self._obs_dump_dir)
        os.makedirs(self._info_workdir)

    def _init_db(self):
        with self._db_conn:
            curs = self._db_conn.cursor()
            try:
                curs.execute('create table if not exists states_{} (state_hash int, step_num int)'.format(self._config_name))
                curs.execute('create table if not exists codecov_{} (seqpt_id int primary key, step_num int)'.format(self._config_name))
                curs.execute('create table if not exists failures_{} (failure text, step_num int)'.format(self._config_name))
                curs.execute('create table if not exists actions_{} (num_valid_actions int, step_num int)'.format(self._config_name))
                curs.execute('create table if not exists time_va_{} (time_valid_actions int, step_num int)'.format(self._config_name))
                curs.execute('create table if not exists time_pa_{} (time_perform_action int, step_num int)'.format(self._config_name))
            finally:
                curs.close()

    def process_info(self, info, step_num):
        # State coverage
        if 'state_hash' in info:
            state_hashes = info['state_hash'][info['_state_hash']]
            with self._db_conn:
                curs = self._db_conn.cursor()
                try:
                    for state_hash in state_hashes:
                        curs.execute('insert into states_{} (state_hash, step_num) values (?, ?)'.format(self._config_name), (int(state_hash), step_num))
                finally:
                    curs.close()

        # Code coverage
        if 'codecov_acv' in info:
            codecov_acvs = info['codecov_acv'][info['_codecov_acv']]
            for i in range(len(codecov_acvs)):
                acv = codecov_acvs[i]
                os.rename(acv, os.path.join(self._info_workdir, 'coverage.json.{}.acv'.format(i)))
            shutil.copy(self._coverage_json_path + '.acv', os.path.join(self._info_workdir, 'coverage.json.acv'))
            shutil.copy(self._coverage_json_path, os.path.join(self._info_workdir, 'coverage.json'))
            try:
                subprocess.run([self._altcover_cmd, 'runner', '--collect', '-r', self._managed_dir], cwd=self._info_workdir, check=True)
            except (FileNotFoundError, subprocess.CalledProcessError) as e:
                raise Exception('failed to process code coverage data: make sure altcover is in your PATH')
            with open(os.path.join(self._info_workdir, 'coverage.json'), 'r') as f:
                cov = json.load(f)
            with self._db_conn:
                curs = self._db_conn.cursor()
                try:
                    for modname, files in cov.items():
                        for filename, classes in files.items():
                            for classname, methods in classes.items():
                                for mname, minfo in methods.items():
                                    for seqpt in minfo['SeqPnts']:
                                        if seqpt['VC'] > 0:
                                            curs.execute('insert or ignore into codecov_{} (seqpt_id, step_num) values (?, ?)'
                                                         .format(self._config_name), (seqpt['Id'], step_num))
                finally:
                    curs.close()

        # Failures
        if 'failures' in info:
            failures = []
            for env_failures in info['failures'][info['_failures']]:
                for fail in env_failures.split(';;;;;'):
                    failures.append(fail)
            with self._db_conn:
                curs = self._db_conn.cursor()
                try:
                    for fail in failures:
                        curs.execute('insert into failures_{} (failure, step_num) values (?, ?)'.format(self._config_name), (fail, step_num))
                finally:
                    curs.close()

        # Time measurement for determining valid actions
        if 'time_valid_actions' in info:
            time_valid_actions = info['time_valid_actions'][info['_time_valid_actions']]
            with self._db_conn:
                curs = self._db_conn.cursor()
                try:
                    for time_va in time_valid_actions:
                        curs.execute('insert into time_va_{} (time_valid_actions, step_num) values (?, ?)'.format(self._config_name), (int(time_va), step_num))
                finally:
                    curs.close()

        # Time measurement for performing chosen action
        if 'time_perform_action' in info:
            time_perform_action = info['time_perform_action'][info['_time_perform_action']]
            with self._db_conn:
                curs = self._db_conn.cursor()
                try:
                    for time_pa in time_perform_action:
                        curs.execute('insert into time_pa_{} (time_perform_action, step_num) values (?, ?)'.format(self._config_name), (int(time_pa), step_num))
                finally:
                    curs.close()

    def process_observation(self, observation, step_num):
        act_mask = observation['action_mask']
        with self._db_conn:
            curs = self._db_conn.cursor()
            try:
                for env_act_mask in act_mask:
                    env_num_valid_actions = int(np.sum(env_act_mask))
                    curs.execute('insert into actions_{} (num_valid_actions, step_num) values (?, ?)'.format(self._config_name), (env_num_valid_actions, step_num))
            finally:
                curs.close()

        if random.random() <= 0.05:
            save_path = os.path.join(self._obs_dump_dir, 'obs_{}.npy'.format(step_num))
            if os.path.exists(save_path):
                os.remove(save_path)
            np.save(save_path, observation)

    def _remove_workdir(self):
        for f in glob.glob(os.path.join(self._info_workdir, '*.acv')) \
               + glob.glob(os.path.join(self._info_workdir, '*.json')):
            os.remove(f)
        os.rmdir(self._info_workdir)

    def close(self):
        if self._db_conn:
            self._db_conn.close()
            self._remove_workdir()

def make_env(game_config, game_config_path, workdir, port, pre_init_port, is_training):
    def _init():
        env_id = 'env_{}'.format(port)
        env_workdir = os.path.abspath(os.path.join(workdir, env_id))
        if not os.path.exists(env_workdir):
            os.makedirs(env_workdir)
        env = gym.make('USC-SQL/UnityEnv-v0',
                     identifier=env_id,
                     game_exe=game_config['game_exe'],
                     env_config=game_config['env_config'],
                     work_dir=env_workdir,
                     game_config_path=game_config_path,
                     port=port,
                     pre_init_port=pre_init_port,
                     training_mode=is_training)
        env = TimeLimit(env, max_episode_steps=game_config['env_config']['time_limit'])
        return env
    return _init

def get_num_actions(symex_db_path):
    try:
        db_conn = sqlite3.connect(symex_db_path)
    except:
        print("Failed to open database " + symex_db_path)
        raise
    try:
        curs = db_conn.cursor()
        with db_conn:
            try:
                res = db_conn.execute('select count(*) from paths')
                return res.fetchone()[0]+1
            finally:
                curs.close()
    except:
        print('Error for {}'.format(symex_db_path))
        raise
    finally:
        db_conn.close()
