# No code from thebasics; interop by runtime reflection only

thebasics (the proximity chat mod) is GPL-2.0, while grug-bot is MIT. Copying any of its source, even small helpers like its VTML utilities, would pull grug-bot under the GPL. So we write our own VTML handling and reach thebasics only at runtime through reflection: no compile-time reference and no declared dependency. grug-bot then works with or without thebasics installed. Contributors should not "simplify" this by referencing `thebasics.dll` or pasting its helpers.
