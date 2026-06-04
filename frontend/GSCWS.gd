extends Node

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)
signal lobby_joined()

var socket := WebSocketPeer.new()

var joined_emitted := false


# func _ready():
#	lobby_joined.connect(_on_lobby_joined)

#	Join_Lobby("6E3680", "Mees")

# func _on_lobby_joined():
#	print("Connected!")
#	Start_Match()


# Updates the websocket and checks for incoming messages
func _process(_delta):
	socket.poll()
	
	if (
		socket.get_ready_state() == WebSocketPeer.STATE_OPEN
		and not joined_emitted
	):
		joined_emitted = true
		lobby_joined.emit()

	while socket.get_available_packet_count() > 0:
		var packet = socket.get_packet()
		var text = packet.get_string_from_utf8()

		_handle_message(text)


func Join_Lobby(LId: String, PId: String):
	socket.connect_to_url(
		"ws://localhost:5025/lobby?lobbyId=%s&playerId=%s"
		% [LId, PId]
	)


# Function to play a card
func Play_Card(card_id: String):
	_Send({
		"action": "PLAY_CARD",
		"card_id": card_id
	})


# Function to draw a card
func Draw_Card():
	_Send({
		"action": "DRAW_CARD"
	})


# Function to start match
func Start_Match():
	_Send({
		"action": "START_MATCH"
	})
	


# Helper function to send data
func _Send(data: Dictionary):
	if socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		print("Socket is not connected.")
		return

	socket.send_text(JSON.stringify(data))


# Interprets the data sent by the server
func _handle_message(text: String):
	var data = JSON.parse_string(text)

	match data["action"]:
		"CARD_PLAYED":
			card_played.emit(
				data["player_id"],
				data["card_id"]
			)

		"CARD_DRAWN":
			card_drawn.emit(
				data["player_id"],
				data["card_count"]
			)
