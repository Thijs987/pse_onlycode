extends Control

@onready var panel = $Panel
@onready var label = $Panel/Label

func _ready():
	panel.hide()

func show_tooltip(card_id):
	tooltip_text(card_id)
	label.reset_size()
	panel.z_index = 1000
	panel.size = label.size
	panel.show()

func hide_tooltip():
	panel.hide()

func tooltip_text(card_id):
	match card_id:
		"blue":
			label.text = "This card is unplayable\nLose it via effects from other cards"
		"cm":
			label.text = "Ends your turn without drawing a card"
		"ddos":
			label.text = "Ends your turn without drawing a card\nNext player has to take 2 turns (stackable)"
		"err":
			label.text = "This card is unplayable\nLose it via effects from other cards"
		"goto":
			label.text = "Give the top card of the drawpile to a player\nUse it as joker for other bad coding habits"
		"imp": # You can still get improved hardware
			label.text = "You must play this card and another card\nThe second card will have no effect"
		"inf":
			label.text = "Give the top card of the drawpile to a player\nYou need 2 of this card to play them"
		"merge":
			label.text = "This card is unplayable\nLose it via effects from other cards"
		"miracle":
			label.text = "Shuffle the cards in the drawing pile"
		"nocom":
			label.text = "Give the top card of the drawpile to a player\nYou need 2 of this card to play them"
		"os":
			label.text = "Look at the bottom card of the drawing pile\nYou can keep it or put it on top of the drawpile"
		"sql":
			label.text = "End your turn without drawing a card\nDecide which player has to take two turns (stackable)"
		"test":
			label.text = "Oops, this card shouldn't be here\nThis card is for tests!"
		"trojan":
			label.text = "Give a card from your hand to another player"
		"vibe":
			label.text = "Give the top card of the drawpile to a player\nYou need 2 of this card to play them"
		_:
			label.text = "Error: this card should not exist"
