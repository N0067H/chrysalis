# chrysails

`chrysails` is a small lab project for studying Compile-After-Delivery. Its Rust CLI generates a Windows command script containing a small C# client. The script compiles the client with the test machine's `csc.exe` when run. Network connectivity and remote diagnostics are still planned.

## Planned features

- [x] Build a small .NET client for a test machine.
- [ ] Start the client by hand and show when it is connected.
- [ ] Connect the Rust app and the client over TCP in a private lab network.
- [ ] Show the client name, operating system, and connection status.
- [ ] Run a short list of approved diagnostic commands on the client.
- [ ] Show each command's output, error, and exit status.
- [ ] Set a time limit for commands and let the user stop a running command.
- [ ] Keep a local record of connection and command events.
- [ ] Close the session cleanly from either side.
