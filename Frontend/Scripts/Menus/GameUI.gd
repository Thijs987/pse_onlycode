extends Node2D

@onready var bg_params: ColorRect = $"../Background/CanvasLayer2/ColorRect"

var card_limit_label: Label
var player_labels = {}

var player_list_container: VBoxContainer
var turns = 1

func _ready() -> void:
	controller.message_updated.connect(_on_message)
	_setup_card_limit_label()

	player_list_container = VBoxContainer.new()
	player_list_container.position = Vector2(20, 60)
	player_list_container.z_index = 1000
	add_child(player_list_container)

	_update_player_list()
	if controller.Last_Message.get("action") == "HAND" or controller.Last_Message.get("action") == "MATCH_STARTED":
		var turn_player = controller.Last_Message.get("data", {}).get("nextPlayer", "")
		if turn_player != null:
			_highlight_turn(turn_player)
			if turn_player == controller.PId:
				bg_params.setAlpha(0.1)
			else:
				bg_params.setAlpha(0.5)

func _setup_card_limit_label() -> void:
	card_limit_label = Label.new()
	card_limit_label.add_theme_font_size_override("font_size", 24)
	card_limit_label.position = Vector2(20, 20)
	card_limit_label.z_index = 1000
	add_child(card_limit_label)

	# MATCH_STARTED is received before this scene loads, so the signal has already
	# fired by the time we connect. Read the initial card limit from the stored last
	# message instead of waiting for a (missed) signal; default to 5 otherwise.
	var limit := 5
	if controller.Last_Message.get("action") == "MATCH_STARTED":
		var sent = controller.Last_Message.get("data", {}).get("cardLimit")
		if sent != null:
			limit = int(sent)
	_update_card_limit(limit)

func _update_card_limit(value: int) -> void:
	if card_limit_label != null:
		card_limit_label.text = "Card limit: " + str(value)

func _on_message(msg):
	if not msg.has("action"):
		return

	var action = msg["action"]

	# Both MATCH_STARTED and DECK_SIZE carry the current card limit. The limit starts
	# at 5 and the backend drops it by 1 each time the draw pile is emptied.
	if action == "MATCH_STARTED" or action == "DECK_SIZE":
		turns = msg.get("data", {}).get("turns")
		var sent = msg.get("data", {}).get("cardLimit")
		if sent != null:
			_update_card_limit(int(sent))

	if action == "PLAYER_JOINED" or action == "MATCH_STARTED" or action == "PLAYER_REJOINED" or action == "HAND":
		_update_player_list()

	if action == "PLAYER_REJOINED":
		var rejoined_player = msg.get("playerId", "")
		_set_player_reconnected(rejoined_player)

	if action == "NEXT_TURN" or action == "CARD_PLAYED":
		turns = msg.get("data", {}).get("turns")
		var current_turn_player = msg.get("data", {}).get("nextPlayer")
		if current_turn_player != null:
			_highlight_turn(current_turn_player)
			if current_turn_player == controller.PId:
				bg_params.setAlpha(0.1)
			else:
				bg_params.setAlpha(0.5)

	if action == "PLAYER_LEFT" or action == "PLAYER_DISCONNECTED":
		var left_player = msg.get("playerId", "")
		_set_player_disconnected(left_player)

	if action == "CARD_LIMIT":
		var eliminated_player = msg.get("playerId", "")
		_set_player_lost(eliminated_player)
		if eliminated_player == controller.PId:
			show_notification("You have been eliminated! Spectating...")
		else:
			show_notification(str(eliminated_player) + " was eliminated!")

	if action == "GAME_OVER":
		var winner = msg.get("playerId", "")
		show_game_over(winner)

func _update_player_list():
	for p_id in controller.All_Player_Ids:
		if not player_labels.has(p_id):
			var new_label = Label.new()
			new_label.text = "🟢 " + str(p_id)
			new_label.add_theme_font_size_override("font_size", 24)

			if p_id == controller.PId:
				new_label.text += " (You)"

			player_list_container.add_child(new_label)
			player_labels[p_id] = new_label
			new_label.add_theme_constant_override("outline_size", 10)

func _highlight_turn(current_player_id: String):
	for p_id in player_labels:
		var lbl = player_labels[p_id]
		lbl.add_theme_color_override("font_color", Color(1, 1, 1))
		lbl.text = lbl.text.replace(">> ", "")

		var index = lbl.text.find(" | Turns: ")
		if index != -1:
			lbl.text = lbl.text.substr(0, index)

	if player_labels.has(current_player_id):
		print(current_player_id)
		var active_lbl = player_labels[current_player_id]
		active_lbl.add_theme_color_override("font_color", Color(1.0, 0.845, 0.392, 1.0))
		active_lbl.text = ">> " + active_lbl.text + " | Turns: " + str(turns)

func _set_player_disconnected(player_id: String):
	if player_labels.has(player_id):
		var lbl = player_labels[player_id]
		lbl.text = lbl.text.replace("🟢", "🟠")
		lbl.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5))

func _set_player_reconnected(player_id: String):
	if player_labels.has(player_id):
		var lbl = player_labels[player_id]
		lbl.text = lbl.text.replace("🟠", "🟢")
		lbl.remove_theme_color_override("font_color")

func _set_player_lost(player_id: String):
	if player_labels.has(player_id):
		var lbl = player_labels[player_id]
		lbl.text = lbl.text.replace("🟢", "🔴")
		lbl.add_theme_color_override("font_color", Color(1, 0, 0))

func show_notification(text_str: String):
	var label = Label.new()
	label.text = text_str
	label.add_theme_font_size_override("font_size", 30)
	label.add_theme_color_override("font_color", Color(1, 0.2, 0.2))
	label.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.position = Vector2(get_viewport_rect().size.x / 2.0 - 200, 100)
	label.custom_minimum_size = Vector2(400, 50)
	label.z_index = 1000
	add_child(label)

	var tween = create_tween()
	tween.tween_property(label, "modulate:a", 0.0, 3.0).set_delay(2.0)
	tween.tween_callback(label.queue_free)

func show_game_over(winner: String):
	var panel = ColorRect.new()
	panel.color = Color(0, 0, 0, 0.8)
	panel.size = get_viewport_rect().size
	panel.z_index = 2000
	add_child(panel)

	var label = Label.new()
	label.text = "GAME OVER\n\nWinner: " + str(winner)
	label.add_theme_font_size_override("font_size", 50)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(label)

	var btn = Button.new()
	btn.text = "Return to Menu"
	btn.add_theme_font_size_override("font_size", 30)
	btn.position = Vector2(get_viewport_rect().size.x / 2.0 - 150, get_viewport_rect().size.y / 2.0 + 150)
	btn.size = Vector2(300, 60)
	panel.add_child(btn)
	btn.pressed.connect(_on_return_pressed)

func _on_return_pressed():
	# Disconnect websocket before returning
	if gscws.socket.get_ready_state() == WebSocketPeer.STATE_OPEN:
		gscws.socket.close()
	controller.Reset_Lobby_State()
	MusicPlayer.play_menu_music()
	SceneLoader.load_scene("uid://ctined7qq8dh2")
