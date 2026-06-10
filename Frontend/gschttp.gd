extends Node2D

signal Lobbies_Received(data) # Signal for passing a list of lobbies
signal Game_Created(data) # Signal for passing the created lobby
signal Login_Completed(data) # Signal that passes the status of the login
signal Register_Completed(data) # Signal that passes the status of registering
signal Leaderboard_Received(data) # Signal that passes the leaderboard

const BASE_URL = "http://localhost:5025"

var P_Name := ""

@onready var Controller = $"../Controller"
@onready var GSCWS = $"../GSCWS"


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
	var lobbies = JSON.parse_string(
		body.get_string_from_utf8()
	)

	Controller.Update_Lobbies(lobbies)


# Creates a game with as host "PId" and connects "PId" to that game
func Create_Lobby(PId: String):
	print("Creating lobby")
	P_Name = PId
	var request = HTTPRequest.new()
	add_child(request)

	request.request_completed.connect(_On_Game_Created)

	var json = JSON.stringify("")
	var headers = ["Content-Type: application/json"]
	request.request(
		"%s/api/lobbies/create?hostId=%s"
		% [BASE_URL, PId],
		headers,
		HTTPClient.METHOD_POST,
		json
	)


# Emits the data received from the server
# use GSCHTTP.game_created.connect(Fnc) to pass them as arguments to Fnc.
func _On_Game_Created(result, response_code, headers, body):
	var json = JSON.parse_string(
		body.get_string_from_utf8()
	)

	var lobby_id = json["lobbyId"]

	print("Created lobby:", lobby_id)

	GSCWS.Join_Lobby(lobby_id, P_Name)
