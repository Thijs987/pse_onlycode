extends Node2D

@onready var discard_pile = $"../DiscardPile"
@onready var hand_reference = $"../PlayerHand"
@onready var pile_node = $"../Pile"
const ATTACK_SCENE = preload("res://Scenes/Attack.tscn")
const OS_MENU_SCENE = preload("res://Scenes/OSMenu.tscn")

const CARD_COLLISION_MASK = 1

var screen_size
var dragging_card
var is_hovering
var card_offsetx
var card_offsety
var turns
var first_combo_card = null # Played blanco
var trojan_selecting_gift = false # Played trojan horse
var imp_hardware_active = false #  Played improved hardware
var tooltip_scene = preload("uid://b0ems5mni4412")
var card_tooltip
var os_active = false


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	hand_reference.next_turn.connect(_newturn)
	screen_size = get_viewport_rect().size
	$"../InputManager".connect("left_mouse_release", on_left_mouse_release)
	card_tooltip = tooltip_scene.instantiate()
	add_child(card_tooltip)


# Runs every frame, card is set to current mouse position with offset
func _process(_delta: float) -> void:
	if dragging_card != null:
		var mouse_pos = get_global_mouse_position()
		dragging_card.position = Vector2(clamp(mouse_pos.x + card_offsetx, 0, screen_size.x),
			clamp(mouse_pos.y + card_offsety, 0, screen_size.y))

func _newturn(player):
	if player != null:
		turns = player

func play_card(card):
	if controller.interaction_disabled:
		return
	if discard_pile == null:
		print("Error: DiscardPile node not found!")
		return
	var discard_area = discard_pile.get_node_or_null("DiscardPileArea")
	if discard_area == null:
		print("Error: DiscardPileArea node not found!")
		return
		
		
	if discard_area.overlaps_area(card.get_node("Area2D")) and controller.PId == turns:
		card.movable = false
		highlight_card(card, false)
		#controller.Play_Card(controller.PId, card.own_card_id)
		#hand_reference.add_card_to_hand(card,0)

		#controller.Play_Card(controller.PId, card.own_card_id)

		var current_id = card.own_card_id
		var played_cards = []
		var data = {}
		var target_id = ""
		var blanco = ["nocom", "goto", "inf", "vibe"]
		
		for card1 in hand_reference.player_hands[0]:
			if "imp" == card1.own_card_id && current_id != "imp":
				print("Hand contains imp, play it")
				card.movable = true
				return false

		print("Card: " + current_id)
		if current_id in blanco:
			if first_combo_card == null: # First blanco card played
				if has_another_blanco(current_id):
					print("Eerste combo-kaart geselecteerd: ", current_id)
					first_combo_card = card

					# Place the card visually
					first_combo_card.position.y -= 30
					first_combo_card.position.x -= 200
					first_combo_card.movable = false

					return true
				else:
					print("You dont have a second blanco card")
					card.movable = true
					return false
			else: # Second blanco card
				# Checks if type matches or at least 1 card is a goto
				if current_id == first_combo_card.own_card_id or current_id == "goto" or first_combo_card.own_card_id == "goto":
					played_cards = [first_combo_card.own_card_id, current_id]
					print("Geldige combo gemaakt! Versturen naar server: ", played_cards)
					target_id = await sql_attack()
					data = {cardId = first_combo_card.own_card_id,
							target = target_id,
							cards = [first_combo_card.own_card_id, current_id]}

					# Play both cards
					hand_reference.remove_card_from_hand(first_combo_card, 0)
					hand_reference.remove_card_from_hand(card, 0)

					first_combo_card.queue_free()
					card.queue_free()

					first_combo_card = null # Reset combo flag
					
					# Select target is a TODO
				else:
					print("Bad combo, cards need to be of same type or 1 has to be nocom")
					card.movable = true
					return false

		elif current_id == "sql":
			played_cards = [current_id]
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()
			target_id = await sql_attack()
			data = {cardId = current_id,
					target = target_id}
		
		elif current_id == "trojan":
			if hand_reference.player_hands[0].size() < 2: # Need to have a card to give
				print("You dont have a card to give")
				card.movable = true
				return false
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()

			print("Choose a card to give to player")
			trojan_selecting_gift = true

			return true

		elif current_id == "imp":
			print("Playcard")
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()

			if hand_reference.player_hands[0].size() == 0: # No other cards
				print("No other cards in hand, play only improved hardware")
				data = {cardId = current_id}
				controller.Play_Card(controller.PId,data)
			else: # Choose another card
				print("Chose card to play without effect")
				imp_hardware_active = true

			return true

		elif current_id == "os":
			if pile_node != null and pile_node.card_count <= 0: # Pile empty?
				print("Cant play card with empty drawpile")
				card.movable = true
				return false
			
			var initial_data = {
				cardId = current_id,
				target = "view"
			}
			controller.Play_Card(controller.PId, initial_data)
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()

			await controller.message_updated

			var server_cards = controller.Last_Data.get("cards", [])
			var kaart_om_te_tonen = "os"
			if server_cards.size() > 0:
				kaart_om_te_tonen = server_cards[0] # Grab card
			
			var decision = await open_source_menu(kaart_om_te_tonen)
			
			var final_data = {
				cardId = current_id,
				target = decision
			}
			controller.Play_Card(controller.PId, final_data)
			
			return true

		else:
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()
			data = {cardId = current_id}

		controller.Play_Card(controller.PId, data)
		return true
	return false

func open_source_menu(card_id_to_show: String):
	os_active = true
	var os_screen = OS_MENU_SCENE.instantiate()
	get_tree().root.add_child(os_screen)
	
	# Vertel het menu welke kaart het moet tonen
	if os_screen.has_method("toon_kaart"):
		os_screen.toon_kaart(card_id_to_show)
	
	get_tree().paused = true 
	var gekozen_keuze = await os_screen.choice_selected
	get_tree().paused = false
	# Voeg kaart toe aan hand
	if gekozen_keuze == "take":
		hand_reference.add_new_card(card_id_to_show, 0)
	
	os_active = false
	return gekozen_keuze


# Code for sql attack card
func sql_attack() -> String:
	var attack_screen = ATTACK_SCENE.instantiate()
	get_tree().root.add_child(attack_screen)
	
	if attack_screen is Control:
		attack_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	var filtered_enemies = []
	for p_id in controller.All_Player_Ids:
		if p_id != controller.PId:
			filtered_enemies.append(p_id)
	
	attack_screen.setup_targets(filtered_enemies)
	get_tree().paused = true # Pauses the game except for the selection menu
	var gekozen_id = await attack_screen.target_selected
	get_tree().paused = false
	return gekozen_id

# Checks if you have a second blanco card
func has_another_blanco(card_type: String) -> bool:
	var count = 0
	var blanco = ["nocom", "goto", "inf", "vibe"]
	for c in hand_reference.player_hands[0]:
		if c.own_card_id == card_type or c.own_card_id == "goto":
			count += 1
		elif card_type == "goto" and c.own_card_id in blanco:
			count += 1
	return count >= 2
		
		

# Starts dragging of current card under mouse.
# input: Card object found using check_at_cursor function
func start_dragging(card):
	if controller.interaction_disabled:
		return
		
		# TROJAN HORSE
	if trojan_selecting_gift:
		trojan_selecting_gift = false # Reset status
		
		var gift_card_id = card.own_card_id
		print("Chosen card: ", gift_card_id)
		
		# Delete card from your hand
		hand_reference.remove_card_from_hand(card, 0)
		card.queue_free()
		var target_id = await sql_attack() 
		
		var data = {cardId = "trojan",
					target = target_id,
					cards = [gift_card_id]}
		
		#var played_cards = ["trojan", gift_card_id]
		controller.Play_Card(controller.PId, data)
		return
		
	if imp_hardware_active:
		imp_hardware_active = false
		
		var sacrifice_card_id = card.own_card_id
		print("Chosen card: ", sacrifice_card_id)

		# Delete card from your hand
		hand_reference.remove_card_from_hand(card, 0)
		card.queue_free()

		#var played_cards = ["imp", sacrifice_card_id]
		
		var data = {cardId = sacrifice_card_id,}

		controller.Play_Card(controller.PId, data)
		return

	if first_combo_card != null: # Player played a blanco card
		var blanco = ["nocom", "goto", "inf", "vibe"]
		if not card.own_card_id in blanco:
			print("Play a blanco card!")
			return

	card.scale = Vector2(1.0, 1.0)
	dragging_card = card
	card.z_index = 99 # Above all other cards
	var card_pos = dragging_card.position
	var mouse_pos = get_global_mouse_position()
	card_offsetx = card_pos.x - mouse_pos.x
	card_offsety = card_pos.y - mouse_pos.y
	card_tooltip.hide_tooltip()

# Calls logic for case of stopping dragging when left mouse button is released
func stop_dragging():
	if dragging_card and dragging_card.movable == true:
		# Als play_card true teruggeeft, stoppen we HIER direct!
		if await play_card(dragging_card):
			dragging_card = null
			return 
			
	var released_card = dragging_card # Temp variable for add_card_to_hand
	if released_card and (released_card.movable == true or released_card.has_meta("pending")):
		released_card.scale = Vector2(1.1, 1.1)
		dragging_card = null

		hand_reference.add_card_to_hand(released_card, 0)
		if released_card.global_position == released_card.hand_position:
			highlight_card(released_card, true)

	dragging_card = null

# Connects the signals for various player actions
func connect_card_signals(card):
	card.connect("hovered", hovered_over_card)
	card.connect("hovered_away", hovered_away_card)

# Calls functions for case of releasing left mouse button
func on_left_mouse_release():
	stop_dragging()
	var card = check_for_card()

# sets is_hovering to true and highlights current card
func hovered_over_card(card):
	if !is_hovering and card.movable == true:
		is_hovering = true
		highlight_card(card, true)

# Checks wether the mouse is currently hovering over a card
# if not, sets is_hovering to false
func hovered_away_card(card):
	highlight_card(card, false)
	var check_card = check_for_card()
	if check_card and card.movable == true:
		highlight_card(check_card, true)
	else:
		is_hovering = false

# Will highlight the card if mouse is hovering over it
func highlight_card(card, hovered):
	if hovered:
		if "is_others" in card:
			if card.is_others == false:
				card.scale = Vector2(1.1, 1.1)
				#if the mouse is below show_tooltip_y, show the tooltip
				var show_tooltip_y = hand_reference.center_screen_y * 2
				show_tooltip_y -= hand_reference.CARD_HEIGHT * 0.2
				if not dragging_card and card.global_position.y > show_tooltip_y:
					card_tooltip.show_tooltip(card.own_card_id)
					card_tooltip.global_position.x = card.global_position.x
					card_tooltip.global_position.y = show_tooltip_y
	else:
		card.scale = Vector2(1.0, 1.0)
		card_tooltip.hide_tooltip()

# Checks wether under the current mouse position is a card and returns
# the collider of the card
func check_for_card():
	var space_state = get_world_2d().direct_space_state
	var parameters = PhysicsPointQueryParameters2D.new()
	parameters.position = get_global_mouse_position()
	parameters.collide_with_areas = true
	parameters.collision_mask = 1
	var result = space_state.intersect_point(parameters)
	var result_size = result.size()
	if (result_size > 0):
		return highest_z(result)
	return null

# returns the card with the highest z value from a list of cards checked
# by check_for_card()
func highest_z(cards):
	var highest_card = cards[0].collider.get_parent()
	var highest_card_z = highest_card.z_index
	for i in range(0, cards.size()):
		var new_card = cards[i].collider.get_parent()
		var new_card_z = new_card.z_index
		if new_card_z > highest_card_z:
			highest_card = new_card
			highest_card_z = new_card_z
	return highest_card
