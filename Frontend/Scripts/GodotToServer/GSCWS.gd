extends Node2D

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)
signal lobby_joined()
signal lobby_left()
signal match_start

var socket := WebSocketPeer.new()
var joined_emitted := false
var is_connecting := false

# Automatically use localhost in Godot Editor, and the actual server for exported builds
var BASE_URL: String = "wss://localhost:6969" if OS.has_feature("editor") else "wss://codegreen-uva.ddns.net"


func _on_lobby_joined():
	print("Connected!")


# Updates the websocket and checks for incoming messages
func _process(_delta):
	socket.poll()
	if (
		socket.get_ready_state() == WebSocketPeer.STATE_OPEN
		and not joined_emitted
	):
		joined_emitted = true
		is_connecting = false
		lobby_joined.emit()
	elif socket.get_ready_state() == WebSocketPeer.STATE_CLOSED:
		if joined_emitted:
			joined_emitted = false
			lobby_left.emit()

	while socket.get_available_packet_count() > 0:
		var packet = socket.get_packet()
		var text = packet.get_string_from_utf8()

		_handle_message(text)


func Join_Lobby(LId: String, PId: String):
	is_connecting = true
	# Tells Godot to trust our Nginx certificate heh
	var tls_options = TLSOptions.client_unsafe()
	var url = "%s/lobby?lobbyId=%s&playerId=%s" % [BASE_URL, LId, PId]
	# Append JWT token for authentication
	url = auth_manager.get_ws_url_with_auth(url)
	socket.connect_to_url(
		url,
		tls_options # <-- Pass the bypass here!
	)

func Leave_Lobby():
	_Send(_Make_Message("LEAVE_LOBBY"))
	if socket.get_ready_state() == WebSocketPeer.STATE_OPEN:
		socket.close()
	joined_emitted = false
	controller.lobby_left.emit()


# Function to play a card
func Play_Card(card_id: String):
	_Send(_Make_Message("PLAY_CARD", {"cardId": card_id}))

# Function to draw a card
func Draw_Card():
	_Send(_Make_Message("DRAW_CARD"))

# Function to start match
func Start_Match(message: Dictionary):
	_Send(_Make_Message("START_MATCH", message))

# Function to add bot
func Add_Bot():
	_Send(_Make_Message("ADD_BOT"))

# Function to kick a player
func Kick_Player(target_id: String):
	_Send(_Make_Message("KICK_PLAYER", {"target": target_id}))

# If Pile == true trojan horse was played
func Gift_Card(OId: String, card_id: String, From_Hand: bool):
	if From_Hand == true:
		_Send(_Make_Message("GIVE_CARD", {"cardId": card_id, "target": OId}))
	else:
		_Send(_Make_Message("GIVE_CARD", {"target": OId}))

# Make outer message
func _Make_Message(action: String, data: Dictionary = {}):
	var message = {"action": action}
	if not data.is_empty():
		message["data"] = data
	return message

# Helper function to send data
func _Send(data: Dictionary):
	if socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		print("Socket is not connected.")
		return

	socket.send_text(JSON.stringify(data))


# Interprets the data sent by the server
func _handle_message(text: String):
	var msg = JSON.parse_string(text)
	if (!msg):
		return
	controller.Update_From_Server(msg)

	match msg["action"]:
		"PLAYER_JOINED":
			#print("PJ")
			pass

		"MATCH_STARTED":
			#print("MS")
			pass

		"ERROR":
			#print("ERR")
			pass

		"CARD_PLAYED":
			#print("CP")
			card_played.emit(
				msg["playerId"],
				msg["data"]["cardId"]
			)

		"CARD_DRAWN":
			#print("CD")
			pass

		"NEXT_TURN":
			#print("NT")
			pass
