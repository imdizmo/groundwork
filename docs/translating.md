# Translating

The editor's words are in catalogues, one per language: a JSON file of texts by key. English,
[`editor/Core/Lang/en.json`](../editor/Core/Lang/en.json), is the source every other catalogue is translated from.

The editor shows the language chosen in **Settings**, by default the computer's when there is a catalogue of it. A text
a catalogue does not have yet shows in English.

## Making a catalogue

1. Copy `en.json` into the `lang` folder of the data folder, named after the language's code: `fr.json`, `de.json`.
2. Set its `lang.name` to the language's own name ("Français").
3. Translate the texts. Keep every `{0}`, `{1}`: they are where the editor puts numbers and names.
4. Choose it in **Settings** and start the editor again.

A catalogue may hold only some texts; the rest stay English.
