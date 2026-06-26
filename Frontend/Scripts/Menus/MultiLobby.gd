extends Control

@onready var lobby_input: LineEdit = $MultiLobbyContainer/BrowserView/VBoxContainer/HBoxContainer3/LineEdit
@onready var in_lobby: Label = $MultiLobbyContainer/InLobbyView/VBoxContainer/CurrentlyInLobby
@onready var player_list_label: Label = $MultiLobbyContainer/InLobbyView/PlayerList
@onready var lobby_item_list: ItemList = $MultiLobbyContainer/BrowserView/LobbyItemList
@onready var refresh_button: Button = $MultiLobbyContainer/BrowserView/RefreshButton
@onready var error_dialog: Label = $MultiLobbyContainer/BrowserView/HBoxContainer2/ErrorLabel
@onready var browser_view: Control = $MultiLobbyContainer/BrowserView
@onready var in_lobby_view: Control = $MultiLobbyContainer/InLobbyView
@onready var main_menu_button: Button = $MultiLobbyContainer/BrowserView/VBoxContainer/MainMenuHBox/MainMenuButton
@onready var tutorial_button: Button = $MultiLobbyContainer/BrowserView/VBoxContainer/HBoxContainer5/TutorialButton
@onready var background: TextureRect = $"Background/CanvasLayer/Background"
@onready var card_setting_box: BoxContainer = $MultiLobbyContainer/InLobbyView/VBoxContainer/CardSettingsBox

@export var game_scene: StringName = &""
@export var create_lobby_button: Button
@export var join_lobby_button: Button
@export var start_lobby_button: Button
@export var card_setting_button: Button
@export var leave_lobby_button: Button

@export var multi_lobby_container: Control
@export var lobby_settings: Control

var player_count = 0
var match_started = false
var lobby_id
var in_lobby_state = false
var created_lobby = false
var rejoin_panel: PanelContainer
var rejoin_lobby_id: String = ""
var bg_start_pos

# Called when the node enters the scene tree for the first time.
var list_container: VBoxContainer
var add_bot_btn: Button

func _ready() -> void:
	bg_start_pos = background.position
	list_container = VBoxContainer.new()
	list_container.position = Vector2(842, 200)
	in_lobby_view.add_child(list_container)

	add_bot_btn = Button.new()
	add_bot_btn.text = "Add Bot"
	add_bot_btn.pressed.connect(_on_add_bot)
	add_bot_btn.visible = false
	list_container.add_child(add_bot_btn)

	create_lobby_button.pressed.connect(_on_create_lobby)
	join_lobby_button.pressed.connect(_on_join_lobby)
	start_lobby_button.pressed.connect(_on_start_lobby)
	refresh_button.pressed.connect(_on_refresh_lobbies)
	leave_lobby_button.pressed.connect(_on_leave_lobby)
	main_menu_button.pressed.connect(_on_main_menu_pressed)
	card_setting_button.pressed.connect(_on_card_settings)
	tutorial_button.pressed.connect(_on_tutorial_pressed)
	lobby_item_list.item_selected.connect(_on_lobby_selected)
	controller.message_updated.connect(_on_message)
	controller.lobbies_updated.connect(_on_lobbies_updated)
	controller.lobby_join_failed.connect(_on_lobby_join_failed)
	controller.lobby_left.connect(_on_lobby_left)
	gscws.lobby_joined.connect(_on_lobby_joined_success)
	gscws.lobby_left.connect(_on_lobby_left)

	_setup_rejoin_dialog()
	controller.rejoin_lobbies_updated.connect(_on_rejoin_lobbies_updated)
	controller.Get_Rejoin_Lobbies(controller.PId)

	update_views()
	controller.Get_Lobbies()

func _process(delta: float) -> void:
	_move_background()

# Creates the moving background
func _move_background() -> void:
	background.position.x -= 0.15
	background.position.y -= 0.3

	if background.position.x <= bg_start_pos.x - 80:
		background.position = bg_start_pos

func _setup_rejoin_dialog() -> void:
	rejoin_panel = PanelContainer.new()
	rejoin_panel.visible = false
	rejoin_panel.set_anchors_preset(Control.PRESET_FULL_RECT)

	var center = CenterContainer.new()
	rejoin_panel.add_child(center)

	var box_bg = PanelContainer.new()
	center.add_child(box_bg)

	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_right", 20)
	margin.add_theme_constant_override("margin_top", 20)
	margin.add_theme_constant_override("margin_bottom", 20)
	box_bg.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 15)
	margin.add_child(vbox)

	var title = Label.new()
	title.text = "Reconnect"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(title)

	var text = Label.new()
	text.text = "You disconnected from an active match. Would you like to rejoin?"
	vbox.add_child(text)

	var hbox = HBoxContainer.new()
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	hbox.add_theme_constant_override("separation", 20)
	vbox.add_child(hbox)

	var btn_rejoin = Button.new()
	btn_rejoin.text = "Rejoin Match"
	btn_rejoin.pressed.connect(_on_rejoin_confirmed)
	hbox.add_child(btn_rejoin)

	var btn_cancel = Button.new()
	btn_cancel.text = "Cancel"
	btn_cancel.pressed.connect(_on_rejoin_cancelled)
	hbox.add_child(btn_cancel)

	add_child(rejoin_panel)

func _on_rejoin_lobbies_updated(lobbies: Array) -> void:
	if lobbies.size() > 0:
		rejoin_lobby_id = lobbies[0].get("lobbyId", lobbies[0].get("LobbyId", ""))
		rejoin_panel.visible = true

func _on_rejoin_confirmed() -> void:
	rejoin_panel.visible = false
	if rejoin_lobby_id != "":
		controller.Join_Lobby(rejoin_lobby_id, controller.PId)

func _on_rejoin_cancelled() -> void:
	if rejoin_lobby_id != "":
		controller.Abandon_Lobby(rejoin_lobby_id, controller.PId)
	rejoin_panel.visible = false
	rejoin_lobby_id = ""

func _on_add_bot():
	controller.Add_Bot()

func _on_lobby_joined_success() -> void:
	in_lobby.text = "Currently in lobby"
	in_lobby_state = true
	update_views()

func _on_lobby_left() -> void:
	in_lobby.text = "Not in lobby"
	in_lobby_state = false
	created_lobby = false
	player_count = 0
	controller.Reset_Lobby_State()
	update_lobby_list()
	update_views()
	controller.Get_Lobbies()

func _on_message(msg):
	if msg["action"] == "MATCH_STARTED" and match_started == false:
		match_started = true
		SceneLoader.load_scene(game_scene)
	elif msg["action"] == "PLAYER_JOINED":
		var p_id = msg.get("playerId", msg.get("PlayerId", ""))
		if p_id == controller.PId:
			controller.player_list[player_count] = controller.PId
			player_count += 1
			update_lobby_list()
		else:
			print("Another player joined")
			controller.player_list[player_count] = p_id
			player_count += 1
			update_lobby_list()
	elif msg["action"] == "PLAYER_LEFT" or msg["action"] == "PLAYER_DISCONNECTED":
		var p_id = msg.get("playerId", msg.get("PlayerId", ""))
		for i in range(player_count):
			if controller.player_list[i].strip_edges() == p_id:
				controller.player_list.remove_at(i)
				controller.player_list.append("")
				player_count -= 1
				update_lobby_list()
				break
	elif msg["action"] == "HOST_TRANSFERRED":
		var new_host = msg.get("playerId", msg.get("PlayerId", ""))
		if new_host == controller.PId:
			created_lobby = true
			update_lobby_list()
	elif msg["action"] == "HAND":
		var p_id = msg.get("playerId", msg.get("PlayerId", ""))
		var new_list = ["", "", "", ""]
		new_list[0] = controller.PId
		var idx = 1
		for p in controller.player_list:
			if p != "" and p != controller.PId:
				new_list[idx] = p
				idx += 1
		controller.player_list = new_list
		player_count = idx
		update_lobby_list()
		SceneLoader.load_scene(game_scene)
	elif msg["action"] == "ERROR":
		if msg.get("playerId", msg.get("PlayerId", "")) == controller.PId:
			print("ERROR: ", msg.get("data", {}).get("error", "Unknown error"))

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _on_create_lobby() -> void:
	print("Create Lobby button pressed! in_lobby_state: ", in_lobby_state)
	if in_lobby_state == false:
		controller.Create_Lobby(controller.PId)
		created_lobby = true


func _on_join_lobby() -> void:
	if in_lobby_state == false:
		lobby_id = lobby_input.text
		if lobby_id:
			controller.Join_Lobby(lobby_id, controller.PId)

func _on_start_lobby():
	if player_count > 1 and created_lobby == true:
		controller.Start_Match(controller.PId)

func update_lobby_list() -> void:
	player_list_label.text = "Current players:"

	for child in list_container.get_children():
		if child != add_bot_btn:
			child.queue_free()

	add_bot_btn.visible = in_lobby_state and created_lobby

	for i in range(player_count):
		var pid = controller.player_list[i].strip_edges()
		if pid == "":
			continue

		var row = HBoxContainer.new()
		var lbl = Label.new()
		lbl.text = pid
		var settings = LabelSettings.new()
		settings.font_color = Color(0, 0, 0, 1)
		lbl.label_settings = settings
		row.add_child(lbl)

		if pid != controller.PId and created_lobby:
			var kick_btn = Button.new()
			kick_btn.text = "Kick"
			kick_btn.pressed.connect(controller.Kick_Player.bind(pid))
			row.add_child(kick_btn)

		list_container.add_child(row)

	list_container.move_child(add_bot_btn, -1)

func _on_refresh_lobbies() -> void:
	controller.Get_Lobbies()

func _on_lobbies_updated(lobbies: Array) -> void:
	print("Received lobbies: ", lobbies)
	lobby_item_list.clear()
	for lobby in lobbies:
		var lobby_id = lobby.get("lobbyId", lobby.get("LobbyId", ""))
		var count = int(lobby.get("playerCount", lobby.get("PlayerCount", 0)))
		var capacity = int(lobby.get("capacity", lobby.get("Capacity", 4)))
		lobby_item_list.add_item("[%d/%d] Lobby %s" % [count, capacity, lobby_id])
		# Store the ID in metadata so we can easily retrieve it
		var idx = lobby_item_list.get_item_count() - 1
		lobby_item_list.set_item_metadata(idx, lobby_id)

func _on_lobby_selected(index: int) -> void:
	var lobby_id = lobby_item_list.get_item_metadata(index)
	lobby_input.text = lobby_id

func _on_lobby_join_failed() -> void:
	error_dialog.text = "Failed to join lobby. It might be full or already started."

func _on_leave_lobby() -> void:
	controller.Leave_Lobby()

func _on_card_settings():
	if in_lobby_view.visible == true:
		in_lobby_view.visible = false
		lobby_settings.visible = true
	else:
		in_lobby_view.visible = true
		lobby_settings.visible = false

func _on_tutorial_pressed():
	#Load tutorial scene
	SceneLoader.load_scene("uid://15klgveacs0r")

func signal_connect(lobby):
	lobby.connect("change_vis", _on_card_settings)

func update_views() -> void:
	browser_view.visible = not in_lobby_state
	if in_lobby_state == true:
		if created_lobby == false:
			card_setting_button.visible = false
		else:
			card_setting_button.visible = true
	in_lobby_view.visible = in_lobby_state

func _on_main_menu_pressed() -> void:
	print("Main Menu button pressed!")
	SceneLoader.load_scene("uid://ctined7qq8dh2")
