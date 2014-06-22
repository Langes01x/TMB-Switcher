TMB-Switcher by Langes01x
============

An automated graphical algorithm switcher made to work with the Trade My Bit site and sgminer 5.0

TradeMyBit: https://pool.trademybit.com/

Sgminer 5.0 Binaries: https://nicehash.com/software/#sgminer

Sgminer 5.0 Source: https://github.com/sgminer-dev/sgminer/tree/v5_0

Git Repository: https://github.com/Langes01x/TMB-Switcher

Bugs/Feature Requests: https://github.com/Langes01x/TMB-Switcher/issues

License: GPLv3. See LICENSE for details.


Donations
============

If you would like to donate some cryptocurrency to the developer of this program
(this is not in any way required for you to use this software)
you may do so using any of the following addresses:

BTC: 15VkLEdNz5RJ2tbrXgshGqSd7VqGGjpu36

LTC: LhAuvDEmBqV7nHwZFFPKEmvYk3jZCzhpHj

DRK: Xg8bkndrqfKx15kEkwnZNwVCGi62vf9WrL


Documentation
============

Example configuration file:

	TMBSwitcherExample.conf

Miner configuration:

	This switcher is made to work with sgminer version 5.0 and will not function properly with older versions.
	
	Older versions may work with the monitoring however the algorithm switching will not function.

Usage:

TMB Switcher Version: 1.0.0.0

Arguments:

    -h	This help message.
	
    -c	The location of the config file. (Default: TMBSwitcher.conf)


Building
============

Requirements:

Visual Studio 2013 (express version should work)

.Net Framework 4.5.1

Open solution file in Visual Studio.

Build > Build Solution

Binaries will be located in <SolutionFolder>\bin\Release



Distribution and Licensing Requirements
============

Binary distributions of this program must include this file (README.md) and the GPLv3 license (LICENSE).
All derivative works must include attribution of the creation of the program to Langes01x and include the donation addresses included within this file.