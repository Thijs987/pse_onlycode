extends Control

# Zet op false bij testen met server ipv local
const USE_LOCAL_MOCK = false 

# Automatically use localhost in Godot Editor, and the actual server for exported builds
var BASE_URL: String = "https://localhost:6969" if OS.has_feature("editor") else "https://codegreen-uva.ddns.net"

const REGISTER_ENDPOINT = "/api/auth/register"
const LOGIN_ENDPOINT = "/api/auth/login"

const MOCK_FILE_PATH = "user://mock_database.json"

# UI REFERENCES - REGISTER
@onready var register_panel: MarginContainer = $"HBoxContainer/RegisterPanel"
@onready var reg_email_input: LineEdit = $HBoxContainer/RegisterPanel/RegisterVBox/RegEmailInput
@onready var reg_username_input: LineEdit = $HBoxContainer/RegisterPanel/RegisterVBox/RegUserInput
@onready var reg_password_input: LineEdit = $HBoxContainer/RegisterPanel/RegisterVBox/RegPasswordInput
@onready var register_button: Button = $HBoxContainer/RegisterPanel/RegisterVBox/RegisterButton
@onready var switch_to_login_button: Button = $"HBoxContainer/RegisterPanel/RegisterVBox/SwitchLoginButton"
@onready var register_status: Label = $HBoxContainer/RegisterPanel/RegisterVBox/RegisterStatusLabel

# UI REFERENCES - LOGIN
@onready var login_panel: MarginContainer = $"HBoxContainer/LoginPanel"
@onready var login_identifier_input: LineEdit = $HBoxContainer/LoginPanel/LoginVBox/LoginIdentifierInput
@onready var login_password_input: LineEdit = $HBoxContainer/LoginPanel/LoginVBox/LoginPasswordInput
@onready var login_button: Button = $HBoxContainer/LoginPanel/LoginVBox/LoginButton
@onready var switch_to_register_button: Button = $"HBoxContainer/LoginPanel/LoginVBox/SwitchRegisterButton"
@onready var login_status: Label = $HBoxContainer/LoginPanel/LoginVBox/LoginStatusLabel

# GENERAL NODES
@onready var return_button: Button = $MarginContainer/ReturnButton
@onready var http_request: HTTPRequest = $HTTPRequest
@onready var background: TextureRect = $"Background/CanvasLayer/Background"

var is_submitting: bool = false
var bg_start_pos

func _ready() -> void:
	register_button.pressed.connect(_on_register_button_pressed)
	login_button.pressed.connect(_on_login_button_pressed)
	return_button.pressed.connect(_on_return_button_pressed)
	switch_to_login_button.pressed.connect(_switch_to_login)
	switch_to_register_button.pressed.connect(_switch_to_register)

	login_password_input.secret = true;
	
	register_status.text = ""
	login_status.text = ""
	bg_start_pos = background.position
	
func _process(delta: float) -> void:
	_move_background()
	
# Creates the moving background
func _move_background() -> void:
	background.position.x -= 0
	background.position.y += 0.5
	
	if background.position.y == bg_start_pos.y + 80:
		background.position = bg_start_pos

# LOGIN
func _on_login_button_pressed() -> void:
	if is_submitting: return
	
	var identifier = login_identifier_input.text.strip_edges()
	var password = login_password_input.text
	
	if identifier == "" or password == "":
		login_status.text = "Please fill in all fields."
		return
		
	set_loading_state(true, "login")
	
	var success: bool
	if USE_LOCAL_MOCK:
		success = await _handle_local_mock_auth(LOGIN_ENDPOINT, {"username": identifier, "password": password}, login_status)
	else:
		success = await _send_auth_request(LOGIN_ENDPOINT, {"username": identifier, "password": password}, login_status)
		
	set_loading_state(false, "login")
	
	if success:
		login_status.text = "Login successful!"
		SceneLoader.load_scene("uid://ctined7qq8dh2")

# REGISTER
func _on_register_button_pressed() -> void:
	if is_submitting: return
	
	var email = reg_email_input.text.strip_edges()
	var username = reg_username_input.text.strip_edges()
	var password = reg_password_input.text
	
	if email == "" or username == "" or password == "":
		register_status.text = "Please fill in all fields."
		return
		
	set_loading_state(true, "register")
	
	var register_data = {
		"email": email,
		"username": username,
		"password": password
	}
	
	var success: bool
	if USE_LOCAL_MOCK:
		success = await _handle_local_mock_auth(REGISTER_ENDPOINT, register_data, register_status)
	else:
		success = await _send_auth_request(REGISTER_ENDPOINT, register_data, register_status)
		
	set_loading_state(false, "register")
	
	if success:
		register_status.text = "Registration complete! Please verify your email."
		reg_email_input.clear()
		reg_username_input.clear()
		reg_password_input.clear()

func _on_return_button_pressed() -> void:
	if not is_submitting:
		SceneLoader.load_scene("uid://ctined7qq8dh2")

# --- SWITCH TO LOGIN SCREEN ---
func _switch_to_login():
	register_panel.visible = false
	login_panel.visible = true

# --- SWITCH TO REGISTER SCREEN ---
func _switch_to_register():
	login_panel.visible = false
	register_panel.visible = true

# --- REAL SERVER NETWORK FUNCTION ---
func _send_auth_request(endpoint: String, data: Dictionary, status_label: Label) -> bool:
	var url = BASE_URL + endpoint
	var headers = ["Content-Type: application/json"]
	var body = JSON.stringify(data)
	
	http_request.set_tls_options(TLSOptions.client_unsafe())
	
	var send_error = http_request.request(url, headers, HTTPClient.METHOD_POST, body)
	if send_error != OK:
		status_label.text = "Network error while connecting."
		return false
		
	var response = await http_request.request_completed
	var response_code = response[1]
	var response_body = response[3].get_string_from_utf8()
	var json_data = JSON.parse_string(response_body)
	
	if response_code == 200:
		if endpoint == LOGIN_ENDPOINT and json_data:
			# Login response contains: { user: {...}, token: "...", expires: "..." }
			if json_data.has("token"):
				var user_data = json_data.get("user", {})
				auth_manager.set_auth(json_data["token"], user_data)
				if json_data.has("expires"):
					auth_manager.set_token_expires(str(json_data["expires"]))
				# Set player ID from user data
				controller.PId = str(user_data.get("username", ""))
			elif json_data.has("username"):
				# Fallback: no JWT configured on server
				controller.PId = str(json_data["username"])
		elif json_data and json_data.has("username"):
			controller.PId = str(json_data["username"])
		return true
	elif response_code == 401 or response_code == 400:
		if json_data and json_data.has("message"):
			status_label.text = json_data["message"]
		elif json_data and json_data.has("Message"):
			status_label.text = json_data["Message"]
		else:
			status_label.text = "Authentication failed (" + str(response_code) + ")."
		return false
	else:
		status_label.text = "Error code: " + str(response_code)
		return false

# --- MOCK DATABASE FUNCTION (LOCAL TESTING) ---
func _handle_local_mock_auth(endpoint: String, data: Dictionary, status_label: Label) -> bool:
	await get_tree().create_timer(0.5).timeout
	
	var local_db = {}
	if FileAccess.file_exists(MOCK_FILE_PATH):
		var file = FileAccess.open(MOCK_FILE_PATH, FileAccess.READ)
		var json_text = file.get_as_text()
		file.close()
		var parsed = JSON.parse_string(json_text)
		if parsed is Dictionary:
			local_db = parsed
			
	if endpoint == REGISTER_ENDPOINT:
		var email = data["email"]
		if local_db.has(email):
			status_label.text = "Email already taken!"
			return false
			
		local_db[email] = {
			"username": data["username"],
			"password": data["password"],
			"id": data["username"]
		}
		
		var file = FileAccess.open(MOCK_FILE_PATH, FileAccess.WRITE)
		file.store_string(JSON.stringify(local_db))
		file.close()
		return true
		
	elif endpoint == LOGIN_ENDPOINT:
		var email = data["email"]
		if not local_db.has(email):
			status_label.text = "Email not found."
			return false
			
		var user_record = local_db[email]
		if user_record["password"] != data["password"]:
			status_label.text = "Wrong password."
			return false
			
		controller.PId = user_record["id"]
		return true
		
	return false

func set_loading_state(busy: bool, mode: String) -> void:
	is_submitting = busy
	register_button.disabled = busy
	login_button.disabled = busy
	return_button.disabled = busy
	
	if busy:
		if mode == "login": login_status.text = "Logging in..."
		if mode == "register": register_status.text = "Creating account..."
