use std::env;
use std::fs;
use std::path::PathBuf;

fn main() {
    println!("cargo:rerun-if-changed=stub/client.cs");
    println!("cargo:rerun-if-changed=stub/run-client.cmd");

    let out_dir =
        PathBuf::from(env::var("OUT_DIR").expect("OUT_DIR is set by Cargo"));

    let build_dir = out_dir
        .ancestors()
        .nth(3)
        .expect("OUT_DIR is inside the Cargo build directory");

    let stub_dir = build_dir.join("stub");
    fs::create_dir_all(&stub_dir)
        .expect("create stub directory beside the executable");

    for name in ["client.cs", "run-client.cmd"] {
        fs::copy(PathBuf::from("stub").join(name), stub_dir.join(name))
            .expect("copy stub template beside the executable");
    }
}
