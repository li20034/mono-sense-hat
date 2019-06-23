#!/bin/bash
grep "Raspbian" /etc/os-release 1>/dev/null 2>&1

if [[ "$?" != 0 ]]; then
    echo "install.sh: you're not running Raspbian, this may not work as intended"
fi

if [[ "`id -u`" != "0" ]]; then
    echo "install.sh: only root can do that!"
    exit 1
fi

echo -n "Waiting for internet connection (Press CTRL+C multiple times to interrupt). . ."
while true; do
    ping -c 1 google.ca 1>/dev/null 2>&1
    if [[ $? -eq 0 ]]; then
        break
    fi
    sleep 1
done
echo ""

trap '' 2

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

pushd "$DIR" 1>/dev/null 2>&1

apt-get install -y sense-hat

if [[ "$?" != 0 ]]; then
    echo "install.sh: sense-hat package cannot be installed. wrapper compilation could fail"
fi

apt-get install -y gcc g++ make

if [[ "$?" != 0 ]]; then
    echo "install.sh: unable to install compilers. wrapper compilation could fail"
fi

rm -rf libsense
git clone https://github.com/moshegottlieb/libsense
cd libsense
patch < ../libsense_fb_gamma.patch
make clean
make
make install
cd ..

rm -f *.so

gcc -fPIC -shared stick.c -s -O3 -o stick.so
g++ -fPIC -shared rtimu_wrapper.cpp -s -O3 -o rtimu_wrapper.so -lRTIMULib
cp *.so /usr/lib

popd 1>/dev/null 2>&1
trap 2
