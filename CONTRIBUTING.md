# Contributing to Groundwork

Thanks for wanting to help. Bug reports, ideas, documentation fixes and code are all welcome.

Groundwork is one of the Jatify tools (Groundwork, Asset Shelf, Prop Generator and the Jatify Launcher). They share one
look and the same way of working, so this guide is nearly the same in each repository.

## Reporting a bug

Open an issue and say:

- what you did, what you expected and what happened instead;
- the version (it is in the window's title) and whether you are on Linux, Windows or macOS;
- the log or a screenshot when you have one.

A map, pack or prop file that reproduces the problem helps most. Only attach files you are allowed to share: do not
upload assets from a paid pack.

**Security problems do not go in a public issue.** Report them privately through the repository's Security tab
(**Report a vulnerability**) (see [SECURITY.md](SECURITY.md)).

## Suggesting a change

Open an issue before writing a larger change, so nobody builds something that will not be merged. Say what you want
to do and why. Small fixes (a typo, a clear bug, a missing check) can go straight to a pull request.

## Building and testing

You need the .NET 10 SDK. The Desktop tests open headless windows, so on Linux they need a display server: `xvfb-run` (from Xvfb) gives them one.

```sh
dotnet build editor/Desktop/Groundwork.Desktop.csproj -c Release
dotnet editor/Desktop/bin/Release/net10.0/Groundwork.dll
```

Run the tests before you open a pull request:

```sh
dotnet test --project editor/tests/Core.Tests/Core.Tests.csproj -c Release
xvfb-run -a dotnet test --project editor/tests/Desktop.Tests/Desktop.Tests.csproj -c Release   # on Windows, without xvfb-run
```

- Every analyzer warning is a build error. Run `dotnet format --include <files>` on the files you changed, not on
  the whole tree.
- The documentation in `docs` is also the editor's Help, and tests check its links: follow
  [docs/dev/writing-docs.md](docs/dev/writing-docs.md). [docs/development.md](docs/development.md) explains the layout.
- The Unreal side is a separate repository, `groundwork-bridge`. A change to a `/groundwork/*` route lands in both
  repositories together, because the editor refuses a bridge of another protocol version.
- Plugins build against `sdk/Groundwork.Sdk`; `sdk/samples/Fences` is the sample.

## Making a pull request

1. Fork the repository and make a branch from `main`. Name it for what it does, for example
   `fix/drawer-resize` or `feat/pack-filter`.
2. Keep one pull request to one change. A fix and an unrelated tidy-up are two pull requests.
3. Add or update a test when you change behaviour. A bug fix gets a test that fails without the fix.
4. Update the documentation and the README when what the user sees changes.
5. Make sure the build has no new warnings and the tests pass.
6. In the pull request, say what changed and how you checked it. For anything visible, add a before and after
   screenshot.

Commit messages are plain sentences that say what changed for the user, as in the existing history, for example
"The objects drawer can be docked on the left". No prefixes or ticket numbers are needed.

## Code style

- C# with nullable reference types on. Follow the style of the file you are editing.
- The interface is built in code, not XAML. Keep it that way.
- Comments say why, not what. Do not leave commented-out code.
- No new dependency for something a few lines can do. If you do need one, say why in the pull request and check that
  its licence allows redistribution.
- Anything that reads a file from someone else (a map, a pack, a preset, a plugin) must treat it as untrusted: stay
  inside its folder, check sizes, and never run code from it without asking the user.

## The look

All four tools use one design. Colours, sizes, radii, type and shadows come from `Tokens.cs`, which is the same file
in every tool with only the namespace changed.

- Use a token. Do not write a colour or a size as a literal in a control.
- Do not edit `Tokens.cs` in one tool only. A change to the tokens is a change to all four tools: open an issue first.
- Filled orange is for the one main action on a screen. A chosen tool or item uses the selected wash.
- Every control needs a visible keyboard focus state and a label a screen reader can read.
- Text is sentence case, in plain words, and says what a button does.

In Groundwork the 3D view fills the window and every piece of chrome floats over it as a card (`Ui.AsCard`): keep
new panels the same, 8 px from the view's edges and from each other. What the 3D view draws (brush rings, selection
outlines, measure lines, water, markers) keeps its own colours, not the tokens. `Tokens.cs` is in `editor/Desktop`.

## Assets and licences

- Only add files you made yourself or whose licence allows redistribution, and list them in `NOTICE.md` with their
  source and licence.
- Never add files taken from a game or from a paid asset pack, and never add generated copies of them.

## Licence

By contributing you agree that your work is released under the MIT licence of this project.

## Getting help

Ask in an issue.
