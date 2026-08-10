#!/usr/bin/env node
// Runs the AI agent with the given prompt.
// Usage: node agent.js "<prompt>"
// Override by replacing this script with your own implementation.

import { spawn } from 'child_process';

const prompt = process.argv[2];
if (!prompt) {
  console.error('ERROR: No prompt provided');
  process.exit(1);
}

const child = spawn('claude', [prompt], { stdio: 'inherit' });
child.on('close', (code) => process.exit(code ?? 1));