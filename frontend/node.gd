extends Node

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)
signal lobby_joined()
signal match_start

var socket := WebSocketPeer.new()

var joined_emitted := false


func _ready():
	lobby_joined.connect(_on_lobby_joined)
	match_start.connect(match_tests)
	Join_Lobby("9B9157", "Player_1")

func match_tests():
	#Draw_Card("Player_1")
	pass

func _on_lobby_joined():
	print("Connected!")
	Start_Match("Player_1")


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
func Draw_Card(PId: String):
	_Send({
		"action": "DRAW_CARD",
		"playerId": PId
	})


# Function to start match
func Start_Match(PId: String):
	var message = _Make_Message("", PId)
	_Send(message)
	
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
	var data = JSON.parse_string(text)
	
	if (text == "Game Started!"):
		match_start.emit()
		return
	
	if (!data):
		return
	#Game Started! message has no data -> action=nil

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
