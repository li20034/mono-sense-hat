#!/bin/bash
if [[ "`id -u`" != "0" ]]; then
    echo "uninstall.sh: only root can do that!"
    exit 1
fi
trap '' 2
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

pushd "$DIR" 1>/dev/null 2>&1

cd libsense
make uninstall
cd ..
rm -rf libsense

rm -f *.so

rm /usr/lib/{rtimu_wrapper,stick}.so

popd 1>/dev/null 2>&1
trap 2
