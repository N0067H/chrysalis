use std::env;
use std::fs;
use std::net::SocketAddr;
use std::path::PathBuf;

const CLIENT_SOURCE: &str = include_str!("../stub/client.cs");
const RUN_SCRIPT: &str = include_str!("../stub/run-client.cmd");

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let mut args = env::args().skip(1);
    if args.next().as_deref() != Some("build") {
        return Err(usage());
    }

    let mut server = None;
    let mut output = None;
    while let Some(option) = args.next() {
        let value = args
            .next()
            .ok_or_else(usage)?;
        match option.as_str() {
            "--server" => server = Some(value),
            "--out" => output = Some(PathBuf::from(value)),
            _ => return Err(usage()),
        }
    }

    let server: SocketAddr = server
        .ok_or_else(usage)?
        .parse()
        .map_err(|_| {
            "--server must be an IP address and port, such as 192.168.1.10:9000"
                .to_string()
        })?;

    let output = output.ok_or_else(usage)?;
    fs::create_dir_all(&output).map_err(|error| error.to_string())?;

    let source =
        CLIENT_SOURCE.replace("{{SERVER_ADDRESS}}", &server.to_string());
    let script = RUN_SCRIPT
        .replace("{{CLIENT_SOURCE}}", source.trim_end())
        .replace("\n", "\r\n");
    fs::write(output.join("run-client.cmd"), script)
        .map_err(|error| error.to_string())?;

    println!(
        "Generated {}",
        output
            .join("run-client.cmd")
            .display()
    );

    println!(
        "On the Windows test machine, run it to compile and start the client."
    );

    Ok(())
}

fn usage() -> String {
    "Usage: chrysails build --server IP:PORT --out DIRECTORY".to_string()
}
