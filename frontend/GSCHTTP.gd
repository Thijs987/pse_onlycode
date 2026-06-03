extends Node

signal Lobbies_Received(data) # Signal for passing a list of lobbies
signal Game_Created(data) # Signal for passing the created lobby
signal Login_Completed(data) # Signal that passes the status of the login
signal Register_Completed(data) # Signal that passes the status of registering
signal Leaderboard_Received(data) # Signal that passes the leaderboard

const BASE_URL = "http://localhost:5025"

# Returns the active Lobbies
func Get_Lobbies():
	var request = HTTPRequest.new()
	add_child(request)

	request.request_completed.connect(_On_Lobbies_Received)

	request.request(
		"%s/api/lobbies/active" % BASE_URL
	)


# Emits the data received from the server.
# Use GSCHTTP.lobbies_received.connect(Fnc) to pass them as arguments to Fnc.
func _On_Lobbies_Received(result, response_code, headers, body):
	var text = body.get_string_from_utf8()

	print("Lobbies:")
	print(text)

	Lobbies_Received.emit(text)


# Creates a game with as host "PId"
func Create_Game(player_id: String):
	var request = HTTPRequest.new()
	add_child(request)

	request.request_completed.connect(_On_Game_Created)

	request.request(
		"%s/lobbies/create?hostId=%s"
		% [BASE_URL, player_id]
	)


# Emits the data received from the server
# use GSCHTTP.game_created.connect(Fnc) to pass them as arguments to Fnc.
func _On_Game_Created(result, response_code, headers, body):
	var text = body.get_string_from_utf8()

	print("Game created:")
	print(text)

	Game_Created.emit(text)
