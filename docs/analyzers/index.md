# Foundry analyzers

`NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers` validates agent and
workflow declarations at compile time. Diagnostic IDs use the `FDRYMAF`
prefix.

The analyzer package covers:

- agent declaration and handoff validity;
- group-chat, sequential, and graph topology constraints;
- function and tool declaration correctness;
- generated workflow naming and registration conflicts.

Each diagnostic reference page documents the condition, severity, and
recommended correction.
