extends Node2D

signal card_played(player_id, card_id)
signal card_drawn(player_id, card_count)
signal lobby_joined()
signal match_start

var socket := WebSocketPeer.new()

var joined_emitted := false

@onready var Controller = $"../Controller"


#func _ready():
	#lobby_joined.connect(_on_lobby_joined)
	#match_start.connect(match_tests)
	#Join_Lobby("9B9157", "Player_1")

#func match_tests():
	##Draw_Card("Player_1")
	#pass

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
	print("OUT:")
	print(data)
	print("")

	socket.send_text(JSON.stringify(data))


# Interprets the data sent by the server
func _handle_message(text: String):
	var msg = JSON.parse_string(text)
	
	if (!msg):
		return
	
	Controller.Update_From_Server(msg)

	match msg["action"]:
		"PLAYER_JOINED":
			print("PJ")
			Start_Match("Player_1")

		"MATCH_STARTED":
			print("MS")

		"ERROR":
			print("ERR")

		"CARD_PLAYED":
			print("CP")
			card_played.emit(
				msg["playerId"],
				msg["data"]["cardId"]
			)

		"CARD_DRAWN":
			print("CD")

		"NEXT_TURN":
			print("NT")
