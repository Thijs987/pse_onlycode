extends Node2D

@onready var discard_pile = $"../DiscardPile"
@onready var hand_reference = $"../PlayerHand"
@onready var pile_node = $"../Pile"
@onready var instruction_label = $"../InstructionLabel"


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
var pending_trojan_card = null
var pending_trojan_gift_id = ""
var pending_trojan_gift_card = null
var trojan_selecting_target = false
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
	
	# Start the game with correct card visuality
	await get_tree().create_timer(0.2).timeout
	update_hand_playability()
	update_instruction_text()

# Runs every frame, card is set to current mouse position with offset
func _process(_delta: float) -> void:
	if dragging_card != null:
		var mouse_pos = get_global_mouse_position()
		dragging_card.position = Vector2(clamp(mouse_pos.x + card_offsetx, 0, screen_size.x),
			clamp(mouse_pos.y + card_offsety, 0, screen_size.y))

func _newturn(player):
	if player != null:
		turns = player
		update_hand_playability()
		update_instruction_text()

# Helper function to safely access a player hand
func _get_my_hand() -> Array:
	if hand_reference == null:
		return []

	if !hand_reference.has_method("get"):
		pass

	if !("player_hands" in hand_reference):
		return []

	if hand_reference.player_hands.is_empty():
		return []

	if hand_reference.player_hands[0] == null:
		return []

	return hand_reference.player_hands[0]

func play_card(card):
	if controller.interaction_disabled:
		return
	if discard_pile == null:
		print("Error: DiscardPile node not found!")
		return
		
	if controller.PId == turns:
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
		var green_cards = ["blue", "err", "merge"]
		
		if current_id in green_cards:
				card.movable = true
				return false
		
		var my_hand = _get_my_hand()

		for card1 in my_hand:
			if card1.own_card_id == "imp" and current_id != "imp":
				print("Hand contains imp, play it")
				card.movable = true
				return false

		#if current_id == "err":
			#card.movable = true
			#return false

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
					update_instruction_text()

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
					
				else:
					print("Bad combo, cards need to be of same type or 1 has to be goto")
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
			if my_hand.size() < 2: # Need to have a card to give
				print("You dont have a card to give")
				card.movable = true
				return false
			pending_trojan_card = card

			hand_reference.remove_card_from_hand(card, 0)

			card.visible = false
			card.movable = false

			print("Choose a card to give to player")
			trojan_selecting_gift = true
			update_instruction_text()

			return true

		elif current_id == "imp":
			print("Playcard")
			hand_reference.remove_card_from_hand(card, 0)
			card.queue_free()

			if my_hand.size() == 0:
				print("No other cards in hand, play only improved hardware")
				data = {cardId = current_id}
				controller.Play_Card(controller.PId, data)
			else: # Choose another card
				print("Chose card to play without effect")
				imp_hardware_active = true
				update_instruction_text()

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
		update_hand_playability()
		update_instruction_text()
		return true
	return false

# Gives you instruction for what to do after playing a card when needed
func update_instruction_text() -> void:
	if instruction_label == null:
		return

	# Not your turm
	var my_turn = (controller.PId == turns) and not controller.interaction_disabled
	if not my_turn:
		instruction_label.display_message("")
		return
		
	if trojan_selecting_gift:
		instruction_label.display_message("TROJAN HORSE: Choose a card from your hand to give away!")
	elif imp_hardware_active:
		instruction_label.display_message("IMPROVED HARDWARE: Choose a card to play without effect!")
	elif first_combo_card != null:
		instruction_label.display_message("COMBO: Play a matching, GOTO or other blank card to finish your combo!")
	else:
		instruction_label.display_message("")

# De opgeschoonde versie voor onderaan cardlogic.gd

func update_hand_playability() -> void:
	if hand_reference == null or hand_reference.player_hands[0].is_empty():
		return
		
	var my_turn = (controller.PId == turns) and not controller.interaction_disabled
	var hand_cards = hand_reference.player_hands[0]
	
	for card in hand_cards:
		var playable = false
		
		if my_turn:
			if trojan_selecting_gift or imp_hardware_active or os_active: # Not in these situations
				playable = false
			else:
				match card.own_card_id:
					"nocom", "goto", "inf", "vibe": # Blanco cards
						if first_combo_card == null:
							playable = has_another_blanco(card.own_card_id)
						else:
							playable = (card.own_card_id == first_combo_card.own_card_id or card.own_card_id == "goto" or first_combo_card.own_card_id == "goto")
							
					"trojan": # Need a card to give
						playable = (hand_cards.size() >= 2)
						
					"os":
						playable = (pile_node != null and pile_node.card_count > 0)
						
					"blue", "err", "merge": # Not playable
						playable = false
						
					_:
						playable = true
						
		if card.has_method("set_playable_visual"):
			card.set_playable_visual(playable)


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
	update_hand_playability()
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
	var my_hand = _get_my_hand()
	var count = 0
	var blanco = ["nocom", "goto", "inf", "vibe"]

	for c in my_hand:
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

	if "is_playable" in card and not card.is_playable:
		# Uitzondering: Als je een Trojan gift aan het kiezen bent, of een IMP sacrifice, 
		# mag je de kaart wél oppakken/klikken!
		if not (trojan_selecting_gift or imp_hardware_active):
			print("Deze kaart mag je nu niet spelen!")

		# TROJAN HORSE
	if trojan_selecting_gift:
		trojan_selecting_gift = false # Reset status
		update_hand_playability()
		
		var gift_card_id = card.own_card_id
		print("Chosen card: ", gift_card_id)
		
		# Delete card from your hand
		pending_trojan_gift_id = card.own_card_id
		pending_trojan_gift_card = card

		hand_reference.remove_card_from_hand(card, 0)

		card.visible = false
		card.movable = false
		update_instruction_text()
		trojan_selecting_target = true
		update_instruction_text()

		var target_id = await sql_attack()
		var attack_node = get_tree().root.get_node_or_null("Attack")
		if attack_node == null:
			return # attack was cancelled by timeout

		# 🔴 BELANGRIJK: als timeout al gereset heeft → stop hier
		if not trojan_selecting_target:
			return

		trojan_selecting_target = false

		var data = {
			cardId = "trojan",
			target = target_id,
			cards = [gift_card_id]
		}

		controller.Play_Card(controller.PId, data)
		
		#var played_cards = ["trojan", gift_card_id]
		controller.Play_Card(controller.PId, data)
		pending_trojan_card = null
		pending_trojan_gift_card = null
		pending_trojan_gift_id = ""
		update_instruction_text()
		return
		
	if imp_hardware_active:
		imp_hardware_active = false
		update_instruction_text()
		
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
		var discard_area = discard_pile.get_node_or_null("DiscardPileArea")
		if discard_area == null:
			print("Error: DiscardPileArea node not found!")
			return
		# Als play_card true teruggeeft, stoppen we HIER direct!
		if discard_area.overlaps_area(dragging_card.get_node("Area2D")):
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
				show_tooltip_y -= hand_reference.CARD_HEIGHT * 0.3
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
