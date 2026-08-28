#!/usr/bin/env node
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const result = spawnSync('python3', [path.join(root, 'scripts', 'capture_screenshots.py')], { cwd: root, stdio: 'inherit' });
process.exitCode = result.status ?? 1;
