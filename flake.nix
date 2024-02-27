{
  description = "LibgenMetalink";

  nixConfig = {
    extra-substituters = [
      "https://programmerino.cachix.org"
    ];
    extra-trusted-public-keys = [
      "programmerino.cachix.org-1:v8UWI2QVhEnoU71CDRNS/K1CcW3yzrQxJc604UiijjA="
    ];
  };

  inputs.nixpkgs.url = "github:IvarWithoutBones/nixpkgs/dotnetmodule-fetch-impure-deps";

  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = {
    self,
    nixpkgs,
    flake-utils,
  }:
    flake-utils.lib.eachDefaultSystem (
      system: let
        pkgs = import nixpkgs {
          inherit system;
        };
        name = "LibgenMetalink";
        sdk = pkgs.dotnet-sdk_6;
      in rec {
        devShell = pkgs.mkShell {
          packages = [sdk] ++ defaultPackage.buildInputs ++ defaultPackage.nativeBuildInputs ++ defaultPackage.propagatedBuildInputs;
        };

        packages."${name}" = pkgs.buildDotnetModule rec {
          pname = name;
          baseName = name;
          version = "0.0.1";
          src = ./fsharp;
          projectFile = "LibgenMetalink.fsproj";

          nugetSha256 = "sha256-xnBD/6buJGCH3Gy3xDs06ycJGs1FZRjZMce0CbcYZLU=";

          executables = ["LibgenMetalink"];
          dotnet-sdk = sdk;
          dotnet-runtime = sdk;
        };

        defaultPackage = packages."${name}";

        apps."${system}" = {
          type = "app";
          program = "${defaultPackage}/bin/${name}";
        };

        defaultApp = apps."${system}";
      }
    );
}
