# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem: report it privately through the repository's Security tab
(**Report a vulnerability**). Say what is affected, the version, and how to reproduce it.

## What is in scope

- Reading a map project from someone else: its `map.json`, heightmap, layers, objects and water files. A map's files
  must stay in its folder, and its Unreal script runs only once the user allows it.
- Plugins: a DLL in the plugins folder runs only once allowed, and again only when the same file is allowed.
- The Claude connection (MCP server): it listens on 127.0.0.1 unless chosen otherwise, and asks every caller for its
  token.
- The live link to the Unreal editor: local only, with the project's token.
