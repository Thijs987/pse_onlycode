extends Node

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)

var socket := WebSocketPeer.new()

# Connect the socket to the server
func _ready():
	var err = socket.connect_to_url("ws://localhost:5025/lobby")

	if err != OK:
		print("Failed to connect:", err)


# Updates the websocket and checks for incoming messages
func _process(_delta):
	socket.poll()

	while socket.get_available_packet_count() > 0:
		var packet = socket.get_packet()
		var text = packet.get_string_from_utf8()

		_handle_message(text)


# Function to play a card
func Play_Card(card_id: String):
	_Send({
		"type": "PLAY_CARD",
		"card_id": card_id
	})


# Function to draw a card
func Draw_Card():
	_Send({
		"type": "DRAW_CARD"
	})


# Function to start match
func Start_Match():
	_Send({
		"type": "START_MATCH"
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

	match data["type"]:
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
