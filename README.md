TankGame
========

A Tank Game

This project is a VS2010 XNA Game Studio 4.0 project using C#.
It makes extensive use of the BEPUPhysics library for physics handling
in the game engine, which may require some additional configuration to run.
The required .dll is included in the project directories but may need to be
located somewhere on the system (I set that up originally over a year ago..)

BEPU Physics website:
http://bepuphysics.codeplex.com/

BEPU uses the Apache 2.0 software license.

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
Pad -- LeftStick

Program Flow (Networking)

Start

Prompt: Host or Join?

Join
- broadcast looking for servers
- listen for responses, add each to list
- write each list, let player choose one
- contact chosen server and register as remote player

Host
- begin listening for server broadcasts and join requests
- start normal gameplay
- if server broadcast received, respond with info
- if join request received, receive info and create new remote player
- listen for remote player input
- transmit updates to registered remote players