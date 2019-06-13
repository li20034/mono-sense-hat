#!/bin/bash
trap '' 2
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

pushd "$DIR" 1>/dev/null 2>&1

if [[ -x "`command -v msbuild`" ]]; then
    msbuild SenseHat.sln /t:Rebuild /p:Configuration=Release
elif [[ -x "`command -v xbuild`" ]]; then
    echo "install.sh: msbuild not found. falling back to deprecated xbuild"
    xbuild SenseHat.sln /t:Rebuild /p:Configuration=Release
else
    echo "install.sh: can't find mono build tools. install mono-devel, msbuild"
    exit 1
fi

mv SenseHat/bin/Release/SenseHat.dll SenseHat.dll

popd 1>/dev/null 2>&1
trap 2
