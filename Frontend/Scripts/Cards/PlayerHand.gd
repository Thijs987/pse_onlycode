extends Node2D

@onready var card_logic = $"../CardLogic"
@onready var turn_label: Label = $"../Background/TurnLabel"
@onready var turn_timer: Timer = $"../TurnTimer"

@export var hand_curve = Curve
@export var rotation_curve = Curve

@export var max_rotation := 5
@export var separation := -10
@export var y_min := 0
@export var y_max := -15

const CARD_SCENE_PATH = "uid://dlb3crw3qdkv2"
const CARD_WIDTH = 120
const CARD_HEIGHT = 300

var player_hands = [[], [], [], []]
var player_amount = 0
var center_screen_y
var center_screen_x

var turn

signal next_turn(player)


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if controller.Last_Message.has("action") and controller.Last_Message["action"] == "MATCH_STARTED":
		var player = controller.Last_Message.get("data", {}).get("nextPlayer")
		if player != null:
			next_turn.emit(player)
			if turn_label != null:
				turn_label.text = str(player)
			if player == controller.PId:
				turn_timer.start()
			else:
				turn_timer.stop()

	change_player_list()
	turn_timer.timeout.connect(_on_timeout)
	controller.message_updated.connect(_on_message)
	center_screen_x = get_viewport().size.x / 2
	center_screen_y = get_viewport().size.y / 2
	for card_id in controller.Player_Hand:
		add_new_card(card_id, 0)
		for i in range(1,4):
			if controller.player_list[i] != "":
				add_new_card("achterkant", i)

func _process(_delta: float) -> void:
	if card_logic and card_logic.dragging_card != null:
		sort_hand()

func _on_message(msg):
	if msg != null:

		if msg["action"] == "NEXT_TURN":
			var player = msg.get("data", {}).get("nextPlayer")
			if player != null:
				next_turn.emit(player)
				turn = player
				if turn_label != null:
					turn_label.text = str(player)
				turn_timer.start()

		if msg["action"] == "CARD_PLAYED":
			var next_player = msg.get("data", {}).get("nextPlayer")
			if next_player != "" and next_player != null:
				next_turn.emit(next_player)
				turn = next_player
				if turn_label != null:
					turn_label.text = str(next_player)
				turn_timer.start()

			var blanco = ["nocom", "goto", "inf", "vibe"]
			# For goto/blanco cards[0-1] are played cards[2] is the given card
			if msg.get("data", {}).get("cardId") in blanco:
				var target = msg.get("data", {}).get("target")
				if target != null and target != "":
					var cards = msg.get("data", {}).get("cards")
					if target == controller.PId && cards != null && cards.size() == 3:
						add_new_card(cards[2], 0)
				
			# Trojan horse puts the sent card in cards[0].
			if msg.get("data", {}).get("cardId") == "trojan":
				var target = msg.get("data", {}).get("target")
				if target != null and target != "":
					var cards = msg.get("data", {}).get("cards")
					if target == controller.PId && cards != null:
						add_new_card(cards[0], 0)
			
			if msg.get("playerId") == controller.PId:
				var played_id = msg.get("data", {}).get("cardId")
				for i in range(player_hands[0].size()):
					if player_hands[0][i].own_card_id == played_id and player_hands[0][i].has_meta("pending"):
						var card = player_hands[0][i]
						player_hands[0].remove_at(i)
						card.queue_free()
						update_card_hand_position(0)
						break

			else:
				#Remove card from the other player that played a card
				var player_number = controller.player_list.find(msg.get("playerId"))
				print(player_number)
				var card = player_hands[player_number][0]
				player_hands[player_number].remove_at(0)
				card.queue_free()
				update_card_hand_position(player_number)

		if msg["action"] == "ERROR":
			if msg.get("playerId") == controller.PId:
				for card in player_hands[0]:
					if card.has_meta("pending") and card.get_meta("pending") == true:
						card.set_meta("pending", false)
						card.modulate.a = 1.0
						card.movable = true

		if msg["action"] == "CARD_DRAWN":
			if msg.get("playerId") != controller.PId:
				add_new_card("achterkant", controller.player_list.find(msg.get("playerId")))


func _on_timeout():
	if turn == controller.PId:
		controller.Draw_Card(controller.PId)

func add_new_card(card_id, player_number):
	var card_scene = preload(CARD_SCENE_PATH)
	var new_card = card_scene.instantiate()

	card_logic.add_child(new_card)

	new_card.name = "Card"
	new_card.set_card(card_id)
	var pile_pos = $"../Pile/PileArea/Pile".global_position
	new_card.global_position = pile_pos
	if player_number == 0:
		new_card.is_others = false
	else:
		new_card.is_others = true
	player_hands[player_number].insert(0, new_card)
	update_card_hand_position(player_number)
	if new_card.own_card_id == "imp": # If you grab an improved hardware, you must play it
		# For now let the card go into your hand
		print("Grabbed improved hardware, must play")
		card_logic.play_card(new_card)

	animate_draw_card(new_card)

func animate_draw_card(card):

	card.scale = Vector2(0.5, 0.5)

	var tween = get_tree().create_tween()
	tween.set_parallel(true)

	tween.tween_property(card, "global_position", card.hand_position, 0.4)
	tween.tween_property(card, "scale", Vector2.ONE, 0.4)

func add_card_to_hand(card, player_number):
	if card not in player_hands[player_number]:
		player_hands[player_number].insert(0, card)
		update_card_hand_position(player_number)
	else:
		if not card.has_meta("pending") or not card.get_meta("pending"):
			card.movable = true
		update_card_hand_position(player_number)
		move_to_position(card, card.hand_position)

func remove_card_from_hand(card, player_number):
	if card in player_hands[player_number]:
		player_hands[player_number].erase(card)
		update_card_hand_position(player_number)

func update_card_hand_position(player_number):
	for i in range(player_hands[player_number].size()):
		var new_position = Vector2(calculate_card_x_position(i, player_number), calculate_card_y_position(i, player_number))
		var moving_card = player_hands[player_number][i]
		rotate_card(moving_card, player_number)
		if moving_card.movable == true:
			moving_card.hand_position = new_position
			var basis_z = player_hands[player_number].size() - i

			if moving_card == card_logic.dragging_card:
				moving_card.z_index = basis_z + 100
			else:
				moving_card.z_index = basis_z
				move_to_position(moving_card, new_position)

func sort_hand():
	var mouse_pos = get_global_mouse_position()
	if mouse_pos.y < 250: # Card too high
		return

	var moving_card = card_logic.dragging_card
	var moving_index = player_hands[0].find(moving_card)
	if moving_index == -1: # Card not found
		return

	if moving_index > 0: # Not left most card
		var left_card = player_hands[0][moving_index - 1]
		if mouse_pos.x < left_card.hand_position.x:
			player_hands[0][moving_index] = left_card
			player_hands[0][moving_index - 1] = moving_card
			update_card_hand_position(0)
			return
	if moving_index < player_hands[0].size() - 1: # Not right most card
		var right_card = player_hands[0][moving_index + 1]
		if mouse_pos.x > right_card.hand_position.x:
			player_hands[0][moving_index] = right_card
			player_hands[0][moving_index + 1] = moving_card
			update_card_hand_position(0)
			return

func calculate_card_x_position(position, player_number):
	if player_number == 1:
		return 0 + CARD_HEIGHT * 0.1
	elif player_number == 3:
		return center_screen_x * 2 - CARD_HEIGHT * 0.1
	var total_width = (player_hands[player_number].size() - 1) * CARD_WIDTH
	var x_offset = center_screen_x + position * CARD_WIDTH - total_width / 2
	return x_offset

func calculate_card_y_position(position, player_number):
	if player_number == 0:
		return center_screen_y * 2 - CARD_HEIGHT * 0.1
	elif player_number == 2:
		return 0 + CARD_HEIGHT * 0.1
	var total_width = (player_hands[player_number].size() - 1) * CARD_WIDTH
	var y_offset = center_screen_y + position * CARD_WIDTH - total_width / 2
	return y_offset

func rotate_card(card, player_number):
	match player_number:
		1:
			card.rotation = PI * 0.5
		2:
			card.rotation = PI
		3:
			card.rotation = PI * 1.5

func move_to_position(card, position):
	var tween = get_tree().create_tween()
	tween.tween_property(card, "position", position, 0.2)

func change_player_list():
	var player_amount = 0
	for player in controller.player_list:
		if player != "":
			player_amount += 1
	var new_list = ["", "", "", ""]
	if player_amount == 2:
		new_list[0] = controller.player_list[0]
		new_list[2] = controller.player_list[1]
		controller.player_list = new_list
	elif player_amount == 3:
		new_list[0] = controller.player_list[0]
		new_list[1] = controller.player_list[1]
		new_list[3] = controller.player_list[2]
		controller.player_list = new_list
