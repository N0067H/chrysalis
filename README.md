# chrysails

`chrysails` is a small lab project for studying Compile-After-Delivery. Its Rust CLI generates a Windows command script containing a small C# client. The script compiles the client with the test machine's `csc.exe` when run. The client connects to a manually started TCP listener and runs approved diagnostics.

## Connection check

On the Rust machine, start the listener and generate the Windows script in separate terminals, using the same address and port:

```sh
cargo run -- serve --bind 192.168.1.10:9000
cargo run -- build --server 192.168.1.10:9000 --out target/client-build
```

Use the Rust machine's reachable lab IP address in place of `192.168.1.10`. Transfer `target/client-build/run-client.cmd` to the Windows test machine and run it by hand. The script compiles and starts the client. The client prints `Status: connected` after the TCP connection succeeds. The listener shows the client's name, OS, and connected or disconnected status. Press Ctrl+C in the client window to close its connection.

## Planned features

- [x] Build a small .NET client for a test machine.
- [x] Start the client by hand and show when it is connected.
- [x] Connect the Rust app and the client over TCP in a private lab network.
- [x] Show the client name, operating system, and connection status.
- [x] Run a short list of approved diagnostic commands on the client.
- [x] Show each command's output, error, and exit status.
- [x] Set a time limit for commands and let the user stop a running command.
- [ ] Keep a local record of connection and command events.
- [ ] Close the session cleanly from either side.

## Approved diagnostics

Specify diagnostics when starting the listener:

```sh
cargo run -- serve --bind 192.168.1.10:9000 --command hostname --command whoami --command ipconfig
```

Each connecting client runs these diagnostics in order and displays output in its
own console. Both sides accept only the exact names `hostname`, `whoami`, and
`ipconfig`, without arguments. The client launches executables from the Windows
system directory without a shell. Without `--command`, only the connection is
checked. Regenerate the client script after updating; older clients do not send
results and are incompatible with diagnostic requests from this listener.
The listener requests one command at a time and displays its stdout, stderr, and
exit status before requesting the next. A launch failure reports stderr and exit
status `-1`. Result fields use UTF-8 with a 1 MiB limit per field; larger results
close the connection with an error.

## Timeout and cancellation

Each diagnostic has a fixed 30-second execution limit enforced by the client.
Type `cancel` and press Enter in the listener terminal to stop the current
diagnostic. Ctrl+C in the client also stops its running diagnostic before
disconnecting. Timeout reports exit status `124`; cancellation reports `130`,
with a reason in stderr and any output already captured. After a timeout or
server cancellation, the listener proceeds to the next configured diagnostic.
If no diagnostic is running, `cancel` has no effect.

Requests carry a command ID so a late cancellation cannot stop a later command.
Regenerate the client script: this request format is incompatible with older
clients. A lost connection also stops the client's running diagnostic.
