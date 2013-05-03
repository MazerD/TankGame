TankGame
========

A Tank Game


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