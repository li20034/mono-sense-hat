#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

trap '' 2
pushd "$DIR" 1>/dev/null 2>&1

mcs fontGen.cs -r:System.Drawing -unsafe

popd 1>/dev/null 2>&1
trap 2
