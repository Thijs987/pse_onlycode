extends Node2D

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)
signal lobby_joined()
signal match_start

var socket := WebSocketPeer.new()
var joined_emitted := false

# Local 
#const BASE_URL = "ws://localhost:6767"
#const BASE_URL = "wss://localhost:6969"

# Pointing to a No-IP.com domain. Its an A record that points towards the server ip.
const BASE_URL = "wss://codegreen-uva.ddns.net"


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
		lobby_joined.emit()
	elif socket.get_ready_state() == WebSocketPeer.STATE_CLOSED:
		joined_emitted = false

	while socket.get_available_packet_count() > 0:
		var packet = socket.get_packet()
		var text = packet.get_string_from_utf8()

		_handle_message(text)


func Join_Lobby(LId: String, PId: String):
	# Tells Godot to trust our Nginx certificate heh
	var tls_options = TLSOptions.client_unsafe()
	
	socket.connect_to_url(
        "%s/lobby?lobbyId=%s&playerId=%s"
		% [BASE_URL, LId, PId],
		tls_options # <-- Pass the bypass here!
	)


# Function to play a card
func Play_Card(PId: String, card_id: String):
	var data = _Make_Data(card_id)
	var message = _Make_Message("PLAY_CARD", PId, data)
	_Send(message)


# Function to draw a card
func Draw_Card(PId: String):
	var message = _Make_Message("DRAW_CARD", PId)
	_Send(message)


# Function to start match
func Start_Match(PId: String):
	var message = _Make_Message("START_MATCH", PId)
	_Send(message)


# If Pile == true trojan horse was played
func Gift_Card(OId: String, card_id: String, From_Hand: bool):
	if From_Hand == true:
		var data = _Make_Data(card_id)
		var message = _Make_Message("GIVE_CARD", OId, data)
		_Send(message)
	else:
		var message = _Make_Message("GIVE_CARD", OId)
		_Send(message)


# Make DataInfo
func _Make_Data(cardId: String = "",
				target: String = "",
				message: String = "",
				nextPlayer = "",
				turns: int = 1,
				error: String = ""):
	var data = {
		"cardId": cardId,
		"target": target,
		"message": message,
		"nextPlayer": nextPlayer,
		"turns": turns,
		"error": error
	}
	return data

# Make outer message and set data to empty DataInfo
func _Make_Message(action: String, PId: String, data: Dictionary = _Make_Data()):
	var message = {
		"action": action,
		"playerId": PId,
		"data": data
	}
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
