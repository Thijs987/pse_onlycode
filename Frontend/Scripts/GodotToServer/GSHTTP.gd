extends Node2D

signal Lobbies_Received(data) # Signal for passing a list of lobbies
signal Game_Created(data) # Signal for passing the created lobby
signal Login_Completed(data) # Signal that passes the status of the login
signal Register_Completed(data) # Signal that passes the status of registering
signal Leaderboard_Received(data) # Signal that passes the leaderboard

# Local
#const BASE_URL = "http://localhost:6767"
#const BASE_URL = "https://localhost:6969"

# Pointing to a No-IP.com domain. Its an A record that points towards the server ip.
const BASE_URL = "https://codegreen-uva.ddns.net"

var P_Name := ""


# Returns the active Lobbies
func Get_Lobbies():
	var request = HTTPRequest.new()
	add_child(request)

	# Tells Godot to trust our Nginx certificate hehe
	request.set_tls_options(TLSOptions.client_unsafe())

	# Tells Godot to trust our Nginx certificate hehe
	request.set_tls_options(TLSOptions.client_unsafe())

	request.request_completed.connect(_On_Lobbies_Received)

	var err = request.request(
		"%s/api/lobbies/active" % BASE_URL
	)
	if err != OK:
		print("HTTPRequest failed to start in Get_Lobbies: ", err)
	else:
		print("Get_Lobbies request started successfully!")


# Emits the data received from the server.
func _On_Lobbies_Received(result, response_code, headers, body):
	print("_On_Lobbies_Received called. Result: ", result, ", Response Code: ", response_code)
	if response_code != 200:
		print("Failed to get lobbies. Error code: ", response_code)
		return

	var lobbies = JSON.parse_string(
		body.get_string_from_utf8()
	)

	if lobbies != null:
		controller.Update_Lobbies(lobbies)


# Creates a game with as host "PId" and connects "PId" to that game
func Create_Lobby(PId: String):
	print("Creating lobby")
	P_Name = PId
	var request = HTTPRequest.new()
	add_child(request)

	# Tells Godot to trust our Nginx certificate hehehe
	request.set_tls_options(TLSOptions.client_unsafe())

	# Tells Godot to trust our Nginx certificate hehehe
	request.set_tls_options(TLSOptions.client_unsafe())

	request.request_completed.connect(_On_Game_Created)

	var json = JSON.stringify("")
	var headers = ["Content-Type: application/json"]
	var err = request.request(
        "%s/api/lobbies/create?hostId=%s"
		% [BASE_URL, PId],
		headers,
		HTTPClient.METHOD_POST,
		json
	)
	print("HTTPRequest.request returned: ", err)


# Emits the data received from the server
func _On_Game_Created(result, response_code, headers, body):
	if response_code != 200:
		print("Failed to create lobby. Error code: ", response_code)
		return
	var json = JSON.parse_string(
		body.get_string_from_utf8()
	)

	if json == null or not json.has("lobbyId"):
		print("Invalid JSON response when creating lobby")
		return

	var lobby_id = json["lobbyId"]

	print("Created lobby:", lobby_id)
	gscws.Join_Lobby(lobby_id, P_Name)
