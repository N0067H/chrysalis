use std::env;
use std::fs;
use std::io::Read;
use std::net::{SocketAddr, TcpListener, TcpStream};
use std::path::PathBuf;
use std::time::Duration;

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
    match args.next().as_deref() {
        Some("build") => build(args),
        Some("serve") => serve(args),
        _ => Err(usage()),
    }
}

fn build(mut args: impl Iterator<Item = String>) -> Result<(), String> {
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

fn serve(mut args: impl Iterator<Item = String>) -> Result<(), String> {
    if args.next().as_deref() != Some("--bind") {
        return Err(usage());
    }

    let address: SocketAddr = args
        .next()
        .ok_or_else(usage)?
        .parse()
        .map_err(|_| "--bind must be an IP address and port".to_string())?;

    if args.next().is_some() {
        return Err(usage());
    }

    let listener =
        TcpListener::bind(address).map_err(|error| error.to_string())?;
    println!(
        "Listening on {}",
        listener
            .local_addr()
            .map_err(|error| error.to_string())?
    );

    for incoming in listener.incoming() {
        let connection = incoming.map_err(|error| error.to_string())?;
        if let Err(error) = show_client(connection) {
            eprintln!("Client connection error: {error}");
        }
    }

    Ok(())
}

fn show_client(mut connection: TcpStream) -> Result<(), String> {
    let peer = connection
        .peer_addr()
        .map_err(|error| error.to_string())?;
    connection
        .set_read_timeout(Some(Duration::from_secs(5)))
        .map_err(|error| error.to_string())?;

    let mut identity = Vec::new();
    loop {
        let mut byte = [0_u8; 1];
        connection
            .read_exact(&mut byte)
            .map_err(|error| error.to_string())?;
        if byte[0] == b'\n' {
            break;
        }
        if identity.len() >= 512 {
            return Err("client identity is too long".to_string());
        }
        identity.push(byte[0]);
    }

    let identity =
        String::from_utf8(identity).map_err(|error| error.to_string())?;
    let (name, os) = identity
        .split_once('\t')
        .ok_or_else(|| "client identity has no OS field".to_string())?;
    if name.is_empty()
        || os.is_empty()
        || name
            .chars()
            .chain(os.chars())
            .any(char::is_control)
    {
        return Err("client identity is invalid".to_string());
    }

    connection
        .set_read_timeout(None)
        .map_err(|error| error.to_string())?;
    println!("Client: {name} | OS: {os} | Status: connected | Peer: {peer}");

    let mut buffer = [0_u8; 1];
    let result = loop {
        match connection.read(&mut buffer) {
            Ok(0) => break Ok(()),
            Ok(_) => {}
            Err(error) => break Err(error.to_string()),
        }
    };
    println!("Client: {name} | OS: {os} | Status: disconnected | Peer: {peer}");
    result
}

fn usage() -> String {
    "Usage:\n  chrysails build --server IP:PORT --out DIRECTORY\n  chrysails serve --bind IP:PORT".to_string()
}
