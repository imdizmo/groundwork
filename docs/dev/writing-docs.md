# Writing the documentation

The Help window shows these pages as they are in `docs/` (everything but `docs/dev/` and `development.md`), so a link
or a heading that works on GitHub works there too.

## A tool's page

Every tool's page has the same parts, in this order, so the Help window's contents look alike from one to the next:

- **Use it**: the steps, from picking the tool to the change on the map.
- **Settings**: each control on the tool's card, by the label the user reads.
- **Shortcuts**: a table of keys and mouse.
- **Tips and limits**: what is easy to miss and what it does not do.

A tool's page is named in `editor/Desktop/HelpTopics.cs`, which the card's `?` opens.

## Style

- Short sentences, plain words; the user's words, not the code's.
- Labels as the editor shows them, in bold: **Save**, **Send to Unreal**.
- Pictures go in `docs/images/` and are taken headless (`--driver`, `shot <file>`), never from a desktop session.
