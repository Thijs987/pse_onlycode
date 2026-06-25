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

signal next_turn(player)


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if controller.Last_Message.has("action") and (controller.Last_Message["action"] == "MATCH_STARTED" or controller.Last_Message["action"] == "HAND"):
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

	for i in range(1, 4):
		var p_id = controller.player_list[i]
		if p_id != "":
			var hand_size = controller.Hand_Sizes.get(p_id, 5)
			for j in range(hand_size):
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
				if turn_label != null:
					turn_label.text = str(player)

				if player == controller.PId:
					turn_timer.start()
				else:
					turn_timer.stop()

		if msg["action"] == "CARD_PLAYED":
			var next_player = msg.get("data", {}).get("nextPlayer")
			if next_player != "" and next_player != null:
				next_turn.emit(next_player)
				if turn_label != null:
					turn_label.text = str(next_player)
				if next_player == controller.PId:
					turn_timer.start()
				else:
					turn_timer.stop()

			var blanco = ["nocom", "goto", "inf", "vibe"]
			# For goto/blanco cards[0-1] are played cards[2] is the given card
			if msg.get("data", {}).get("cardId") in blanco:
				var target = msg.get("data", {}).get("target")
				if target != null and target != "":
					var cards = msg.get("data", {}).get("cards")
					if target == controller.PId && cards != null && cards.size() == 3:
						add_new_card(cards[2], 0)
					if target != controller.PId:
						add_new_card("achterkant", controller.player_list.find(target))

			# Trojan horse puts the sent card in cards[0].
			if msg.get("data", {}).get("cardId") == "trojan":
				var target = msg.get("data", {}).get("target")
				if target != null and target != "":
					var cards = msg.get("data", {}).get("cards")
					if target == controller.PId && cards != null:
						add_new_card(cards[0], 0)
					if target != controller.PId:
						add_new_card("achterkant", controller.player_list.find(target))

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
				var cardId = msg.get("data", {}).get("cardId")
				var player_number = controller.player_list.find(msg.get("playerId"))
				print(player_number)
				if player_number != -1:
					if cardId in blanco or cardId == "trojan":
						var target = msg.get("data", {}).get("target")
						if player_hands[player_number].size() > 0:
							var card = player_hands[player_number][0]
							player_hands[player_number].remove_at(0)
							card.queue_free()
							update_card_hand_position(player_number)
						if player_hands[player_number].size() > 0:
							var card = player_hands[player_number][0]
							player_hands[player_number].remove_at(0)
							card.queue_free()
							update_card_hand_position(player_number)
					elif cardId == "os":
						var take_or = msg.get("data", {}).get("target")
						if take_or == "top":
							if player_hands[player_number].size() > 0:
								var card = player_hands[player_number][0]
								player_hands[player_number].remove_at(0)
								card.queue_free()
								update_card_hand_position(player_number)
					#improved hardware can put down 1 or 2 cards
					#based on if hand is empty if card is picked up
					elif cardId == "imp":
						var cards = msg.get("data", {}).get("cards")
						for c in cards:
							if player_hands[player_number].size() > 0:
								var card = player_hands[player_number][0]
								player_hands[player_number].remove_at(0)
								card.queue_free()
								update_card_hand_position(player_number)
					else:
						if player_hands[player_number].size() > 0:
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
	print("TIMEOUT")
	print("trojan_selecting_gift = ", card_logic.trojan_selecting_gift)
	print("trojan_selecting_target = ", card_logic.trojan_selecting_target)
	if card_logic == null:
		controller.Draw_Card(controller.PId)
		return

	# If a blanco card is played
	if card_logic.first_combo_card != null:
		print("TIMER OUT: Didnt finish combo, take card(s) back.")
		var card_to_reset = card_logic.first_combo_card
		card_logic.first_combo_card = null
		
		card_to_reset.movable = true
		add_card_to_hand(card_to_reset, 0)
		
		controller.Draw_Card(controller.PId)
		return

	# Trojan horse
	# TROJAN GIFT TIMEOUT (BELANGRIJK)
	if card_logic.trojan_selecting_gift:
		print("TIMER OUT: Trojan gift not selected -> rollback")

		card_logic.trojan_selecting_gift = false

		if card_logic.pending_trojan_card:
			card_logic.pending_trojan_card.visible = true
			card_logic.pending_trojan_card.movable = true
			add_card_to_hand(card_logic.pending_trojan_card, 0)
			card_logic.pending_trojan_card = null

		# reset everything
		card_logic.pending_trojan_gift_id = ""
		card_logic.pending_trojan_gift_card = null

		controller.Draw_Card(controller.PId)
		return
		# Giftkaart teruggeven
		if card_logic.pending_trojan_gift_id != "":
			add_new_card(card_logic.pending_trojan_gift_id, 0)

			card_logic.pending_trojan_gift_id = ""

		var attack_node = get_tree().root.get_node_or_null("Attack")
		if attack_node:
			attack_node.queue_free()

		get_tree().paused = false

		controller.Draw_Card(controller.PId)
		return
	if card_logic.trojan_selecting_target:
		print("TIMER OUT: Closing Attack screen (Trojan cancelled)")

		card_logic.trojan_selecting_target = false

		# 🔴 FORCE CLOSE ATTACK SCREEN
		var attack_node = get_tree().root.get_node_or_null("Attack")
		if attack_node:
			attack_node.queue_free()

		get_tree().paused = false

		# restore card if needed
		if card_logic.pending_trojan_card:
			card_logic.pending_trojan_card.visible = true
			card_logic.pending_trojan_card.movable = true
			add_card_to_hand(card_logic.pending_trojan_card, 0)
			card_logic.pending_trojan_card = null

		if card_logic.pending_trojan_gift_card:
			card_logic.pending_trojan_gift_card.visible = true
			card_logic.pending_trojan_gift_card.movable = true
			add_card_to_hand(card_logic.pending_trojan_gift_card, 0)
			card_logic.pending_trojan_gift_card = null

		card_logic.pending_trojan_gift_id = ""

		controller.Draw_Card(controller.PId)
		return

	# Improved hardware
	if card_logic.imp_hardware_active:
		print("TIMER OUT: No card chosen for automatic selection. Automatic choice")
		card_logic.imp_hardware_active = false
		
		var chosen_card = null
		var bricks = ["blue", "err", "merge"]
		var hand_cards = player_hands[0]

		if hand_cards.size() > 0:
			# First check for bricks
			for card in hand_cards:
				if card.own_card_id in bricks:
					chosen_card = card
					break

			if chosen_card == null:
				var random_index = randi() % hand_cards.size()
				chosen_card = hand_cards[random_index]
		
		# Found card to force
		if chosen_card != null:
			var sacrifice_id = chosen_card.own_card_id
			print("Forced card for Improved Hardware: ", sacrifice_id)

			remove_card_from_hand(chosen_card, 0)
			chosen_card.queue_free()

			var data = {cardId = sacrifice_id}
			controller.Play_Card(controller.PId, data)

			card_logic.update_hand_playability()
			card_logic.update_instruction_text()
		else:
			controller.Draw_Card(controller.PId)
			
		return

	# Open source
	if card_logic.os_active:
		print("TIMER OUT: No choice make. Closing menu")
		card_logic.os_active = false
		
		# Searche and delete os screen
		var os_node = get_tree().root.get_node_or_null("OSMenu")
		if os_node:
			print("OSMenu found and deleted")
			if os_node.has_signal("choice_selected"):
				os_node.choice_selected.emit("keep") 
			os_node.queue_free()
			
		get_tree().paused = false
		return

	# Close other screens just in case
	var backup_attack = get_tree().root.get_node_or_null("Attack")
	if backup_attack:
		backup_attack.queue_free()
		get_tree().paused = false

	# Default
	print("TIMER OUT: End turn.")
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
	var number_of_players = controller.All_Player_Ids.size()
	if number_of_players == 0:
		return

	var local_idx = controller.All_Player_Ids.find(controller.PId)
	if local_idx == -1:
		return

	var relative_list = ["", "", "", ""]
	for i in range(number_of_players):
		relative_list[i] = controller.All_Player_Ids[(local_idx + i) % number_of_players]

	var final_list = ["", "", "", ""]
	if number_of_players == 2:
		final_list[0] = relative_list[0]
		final_list[2] = relative_list[1]
	elif number_of_players == 3:
		final_list[0] = relative_list[0]
		final_list[1] = relative_list[1]
		final_list[3] = relative_list[2]
	elif number_of_players >= 4:
		for i in range(4):
			final_list[i] = relative_list[i]

	controller.player_list = final_list
