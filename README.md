# LibgenMetalink

## Building

This project uses Nix (you will need to enable flakes) and you can accelerate builds by using the binary cache `programmerino` with the command:
```
cachix use programmerino
```

and ultimately build with `nix build` which will place a binary/library in `result`
## Running

Simply pass the link of a Library Genesis page for the book (tested with libgen.rs), and it will output a string for the metalink. This can be piped into a file for use.