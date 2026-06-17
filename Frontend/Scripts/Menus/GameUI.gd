extends Node2D

func _ready() -> void:
	controller.message_updated.connect(_on_message)

func _on_message(msg):
	if not msg.has("action"):
		return

	if msg["action"] == "CARD_LIMIT":
		var eliminated_player = msg["playerId"]
		if eliminated_player == controller.PId:
			show_notification("You have been eliminated! Spectating...")
		else:
			show_notification(str(eliminated_player) + " was eliminated!")

	if msg["action"] == "GAME_OVER":
		var winner = msg["playerId"]
		show_game_over(winner)

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
	SceneLoader.load_scene("uid://ctined7qq8dh2")
