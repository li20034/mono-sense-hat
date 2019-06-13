#!/bin/bash
trap '' 2
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

pushd "$DIR" 1>/dev/null 2>&1
msbuild SenseHat.sln /t:Rebuild /p:Configuration=Release

popd 1>/dev/null 2>&1
trap 2
