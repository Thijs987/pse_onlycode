When the host calls START_GAME, it creates a match with the current players in the lobby in the turn cycle.

For testing

- Get all active lobbies:
  GET localhost:5025/api/lobbies/active

Response example no lobbies:
[]

With one lobby:
[
{
"lobbyId": "ED2787",
"playerCount": 0
}
]

- Create a lobby with host Player_1:
  POST localhost:5025/api/lobbies/create?hostId=Player_1

Response example:
{
"lobbyId": "ED2787"
}

- Join a lobby
  ws://localhost:5025/lobby
  Message:
  Trivial

Params example:
lobbyId = ED2787
playerId = Player_1

Response example:
{
"action": "PLAYER_JOINED",
"playerId": "Player_1",
"data": "Player_1 has joined the game!"
}

- Start game
  ws://localhost:5025/lobby
  Message:
  {
  "action": "START_GAME",
  "playerId": "Player_1"
  }

Response:
Game Started!

- Draw card
  ws://localhost:5025/lobby
  Message:
  {
  "action": "DRAW_CARD",
  "playerId": "Player_1"
  }

Response personal (1 is the name of the card):
Got 1

Response broadcast to LOBBY (data: NextPlayer, NTurns):
{
"action": "CARD_DRAWN",
"playerId": "Player_1",
"data": "Player_2,1"
}
