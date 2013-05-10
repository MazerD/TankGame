TankGame
========

ComS 486 Spring 2013
A Tank Game
by Tyler Mixson

This project is a VS2010 XNA Game Studio 4.0 project using C#.
It makes use of the BEPUPhysics library for physics handling
in the game engine, which may require some additional configuration to run.
The required .dll is included in the project directories but may need to be
located somewhere on the system (I set that up originally over a year ago..)

BEPU Physics website:
http://bepuphysics.codeplex.com/

BEPU uses the Apache 2.0 software license.

GamePad controls player 2, controls are printed on-screen

Keyboard controls player 1, controls as follows:
I - Forward  K - Reverse  J - Left  L - Right  U - Turn Left  O - Turn Right
Spacebar - Fire Cannon  Enter - Fire Minigun
R - Reset tank1  T - Reset tank2
PageUp - Nuke the map  PageDown - Rebuild Towers

RShift + 1 - Client Mode   RShift + 2 - Server Mode  // Read about this below

Ports
Server Find Broadcasts: 11500
Remote Player -> Server: 11501
Server -> Remote Player: 11502

Server Search: "TankGameServerFind"
Server Response: "TankGameServerResponse"

Player Register
PlayerRegister::MyIP::MyPort?::PlayerName

Input Packet Format
PlayerID::ControlType::
Pad -- LeftStickX::LeftStickY::RightStick::X::RightStickY

Weapon Fire
PlayerID::Cannon
toggles the 'primed' boolean for the given tank, causes weapon fire on next tick

PlayerID::MinigunOn OR PlayerID::MinigunOff
toggles the trigger state for the given tank's minigun control
fire control code handles actual rate of fire given cooldown settings

Program Flow (Networking)

Application Start

Begins in Local mode running a local simulation with just the two local players

Player pushes RShift + 1 to enter client mode or RShift + 2 to enter server mode

Client
- broadcast a Server Search packet
- listen for responses, add each to list
	// currently this just latches on to the first response since I don't have a fancy lobby
- write each server in list, let player choose one
- contact chosen server and register as remote player
	// sends a Player Register packet to the server in question
	// server runs HandleNewPlayer()

Host
- begin listening for server broadcasts and join requests
- start normal gameplay		// really just continue the local sim that was already running
- if server broadcast received, respond with info
- if join request received, receive info and create new remote player
- listen for remote player input	// via receiveClientUpdate
- transmit updates to registered remote players	// each member of 'remotes' linked list