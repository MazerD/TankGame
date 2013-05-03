TankGame
========

A Tank Game

Ports
Server Find Broadcasts: 11500
Remote Player -> Server: 11501
Server -> Remote Player: 11502

Server Search
Ping?

Server Response
YODAWGIHEARDYOULIKETANKS

Player Register
TANKS::MyIP::MyPort?::PlayerName

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