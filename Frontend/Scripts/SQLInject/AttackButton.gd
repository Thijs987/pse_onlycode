extends TextureButton

signal button_clicked(player_id) # Een eigen signaal om het ID door te geven

var target_player_id := ""

func _ready() -> void:
	# Als er op DEZE knop geklikt wordt, voeren we de functie hieronder uit
	pressed.connect(_on_pressed)

func setup_box(player_name: String, player_id: String) -> void:
	$Label.text = player_name
	target_player_id = player_id

func _on_pressed() -> void:
	# Schiet ons eigen signaal af mét het opgeslagen ID erbij
	button_clicked.emit(target_player_id)
