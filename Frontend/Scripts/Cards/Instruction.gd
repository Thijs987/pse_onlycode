extends CanvasLayer

@onready var label: Label = $Panel/Label

func _ready() -> void:
	# Zorgt ervoor dat dit overal BOVENOP tekent
	layer = 2000
	visible = false # Start onzichtbaar

# Dit is de functie die we gaan aanroepen vanuit cardlogic.gd
func display_message(new_text: String) -> void:
	if label == null:
		return
		
	label.text = new_text
	
	# Als de tekst leeg is, verbergen we de hele CanvasLayer, anders tonen we hem
	if new_text.is_empty():
		visible = false
	else:
		visible = true
