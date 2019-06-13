# mono-sense-hat
Source code for Sense HAT Mono .NET library

## Description
This library is designed to be used for interaction with the Sense HAT in .NET languages. The code is *mostly* compatible with the [python-sense-hat](https://github.com/RPi-Distro/python-sense-hat) library.

## Dependencies
Testing has only been done with Raspbian.
The default version of mono in Raspbian's package repositories may not be compatible with the library. We recommend following the instructions found [here](https://www.mono-project.com/download/stable/#download-lin-raspbian) to install a compatible version. The mono-devel package must be installed for building. Installation of the msbuild package is also strongly recommended but it may not be required.

The following dependancies are automatically retrieved by installation script.

[RTIMULib](https://github.com/RPi-Distro/RTIMULib)

[Libsense](https://github.com/moshegottlieb/libsense)

## Installation
  1. Clone/Download the repository
  2. Navigate to the LowLevelWrappers directory
  3. Run install.sh as root
  4. Navigate to the SenseHat directory
  5. Run build.sh
```bash
$ git clone https://github.com/li20034/mono-sense-hat
$ cd mono-sense-hat/LowLevelWrappers
$ sudo ./install.sh
$ cd ../SenseHat
$ ./build.sh
```

## Usage
The .dll file that is generated by build.sh can be added as a reference in .NET projects. Full documentation of the library is not available at this time, however the source code is thoroughly commented.

## Authors and Contributors
Written by Zonggao Li and Carson Hall as part of a school project.
Special thanks to the other members of the LittleMen (Muntaqim Rahman, Kelvin Zhu and Wayne Zhu) for their assistance in debugging, testing and suggesting changes. 

## Contributing
We are very open to contributions to help us add new features, bug fixes or for creating/updating proper documentation for the project. Testing on other platforms would also be wonderful.

## License
[MIT](https://choosealicense.com/licenses/mit/)
