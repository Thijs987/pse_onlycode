# Code Green

## Rules:

Each player starts the game with 3 cards their hand. During a turn, players can play as many cards as they like.
A player's turn ends when they draw a card from the draw pile, or an effect of a card they played ends their turn.
A player loses when at the end of their turn, there are more cards in their hand than the limit allows.
The limit starts at 5, and reduces by 1 every time the drawpile is empty.
When the drawpile is empty, the cards on the discard pile are shuffled and are placed on the draw pile.
To see the effects of each individual card, see the project wiki.

## Technical information:

If you download the game from a release, you need an eduVPN connection to the University of Amsterdam in order to play the game.
The game will will connect to the servers of the UvA, where you can play globally with other players.
If you download the project from a branch, you can localhost it. When run as debug in the Godot editor, it will automatically localhost.
To localhost, you need to run the backend. First, you need to download the .env file and place it in the root folder of the project.
However, some critical information is missing which you should create yourself, otherwise it would be a security issue for us.
Then, you need to run the set-user-secrets.sh script in the folder Backend/scripts to get access to the database and mailserver.
You need to do these steps only once when downloading a new project.
Finally, you can run the localhost server using ```dotnet run``` in the folder Backend/src. This will run the localhost server with HTTPS.
If you want to run it with HTTP, use ```--launch-profiles "http"```. Launching with HTTPS uses port 6969 and HTTP uses port 6767.
These ports are selected in Backend/src/Properties/launchSettings.json

Note on HTTPS/SSL Warnings:
When running the server, you may see a warning stating that the ASP.NET Core developer certificate is not trusted. This happens because .NET automatically generates a local self-signed certificate for development. Because it was not issued by a public Certificate Authority, it gets flagged it as untrusted until you explicitly trust it. The reason for doing this was that we couldn’t reach any public certificate authorities on our server. 

## Tech Stack:
Godot 4.6.3\
C#.Net\
SMTP\
Postgresql
