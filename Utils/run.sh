#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
trap '' 2
pushd "$DIR" 1>/dev/null 2>&1

if [[ ! -f "sense_hat_text.png" ]] || [[ ! -f "sense_hat_text.txt" ]]; then
    echo "$ rm -rf python-sense-hat"
    rm -rf python-sense-hat
    echo "$ git clone https://github.com/RPi-Distro/python-sense-hat"
    git clone https://github.com/RPi-Distro/python-sense-hat
    echo "$ cp python-sense-hat/sense_hat/sense_hat_text.{png,txt} ."
    cp python-sense-hat/sense_hat/sense_hat_text.{png,txt} .
    echo "$ rm -rf python-sense-hat"
    rm -rf python-sense-hat
fi

if [[ ! -f "fontGen.exe" ]]; then
    echo "$ ./compile.sh"
    bash compile.sh
fi

echo "$ ./fontGen.exe > font.cs"
mono fontGen.exe > font.cs

popd 1>/dev/null 2>&1
trap 2
