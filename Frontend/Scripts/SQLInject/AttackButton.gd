extends TextureButton

signal button_clicked(player_id) # Een eigen signaal om het ID door te geven

var target_player_id := ""

func _ready() -> void:
	pressed.connect(_on_pressed)

func setup_box(player_name: String) -> void:
	$Label.text = player_name
	target_player_id = player_name

func _on_pressed() -> void:
	button_clicked.emit(target_player_id)
