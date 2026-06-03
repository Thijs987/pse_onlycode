extends Node

var socket := WebSocketPeer.new()

func _Ready():
	var err = socket.connect_to_url("ws://localhost:5025/lobby")

	if err != OK:
		print("Failed to connect:", err)


func _Process(_delta):
	socket.poll()

	if socket.get_ready_state() == WebSocketPeer.STATE_OPEN:
		_Send({
			"type": "TEST"
		})
		
		set_process(false)
		


# Function to play a card
func Play_Card(card_id: int):
	_Send({
		"type": "PLAY_CARD",
		"card_id": card_id
	})


# Function to draw a card
func Draw_Card():
	_Send({
		"type": "DRAW_CARD"
	})


# Helper function to send data
func _Send(data: Dictionary):
	socket.send_text(JSON.stringify(data))
