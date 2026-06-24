extends Control

@onready var CM: LineEdit = $VBoxContainer/HBoxContainer/NoCleanMerge
@onready var Ddos: LineEdit = $VBoxContainer/HBoxContainer2/NoDdos
@onready var SQL: LineEdit = $VBoxContainer/HBoxContainer3/NoSQL
@onready var MS: LineEdit = $VBoxContainer/HBoxContainer4/NoSort
@onready var Err: LineEdit = $VBoxContainer/HBoxContainer5/NoErr
@onready var Goto: LineEdit = $VBoxContainer/HBoxContainer6/NoGoto
@onready var IH: LineEdit = $VBoxContainer/HBoxContainer7/NoHardware
@onready var Unplayable: LineEdit = $VBoxContainer/HBoxContainer8/NoUnplayable
@onready var TH: LineEdit = $VBoxContainer/HBoxContainer9/NoTrojan
@onready var OpS: LineEdit = $VBoxContainer/HBoxContainer10/NoOpenSource

@onready var Result: Label = $VBoxContainer/HBoxContainer13/Result

@onready var SubmitButton = $VBoxContainer/HBoxContainer11/SubmitButton
@onready var ResetButton = $VBoxContainer/HBoxContainer11/ResetButton
@onready var BackButton = $VBoxContainer/HBoxContainer12/BackButton
@onready var background: TextureRect = $Background/CanvasLayer/Background

signal change_vis

var StanSet = { "CM" : 4,
				"Ddos" : 2,
				"SQL" : 2,
				"MS" : 2,
				"Err" : 4,
				"Goto" : 4,
				"IH" : 4,
				"Unplayable" : 6,
				"TH" : 4,
				"OpS" : 2
				}

var CusSet = StanSet.duplicate()

var Fields = {}
var bg_start_pos


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	bg_start_pos = background.position
	Fields = { "CM": CM,
			   "Ddos": Ddos,
			   "SQL": SQL,
			   "MS": MS,
			   "Err": Err,
			   "Goto": Goto,
			   "IH": IH,
			   "Unplayable": Unplayable,
			   "TH": TH,
			   "OpS": OpS
			   }
	
	SubmitButton.pressed.connect(_on_submit)
	ResetButton.pressed.connect(_on_reset)
	BackButton.pressed.connect(_on_back)
	
	for card_name in StanSet:
		Fields[card_name].text = str(StanSet[card_name])
	
	if get_parent().has_method("signal_connect"):
		get_parent().signal_connect(self)
	
	if get_parent().has_method("change_set"):
		get_parent().signal_connect(self)

func _process(delta: float) -> void:
	_move_background()
		
# Creates the moving background
func _move_background() -> void:
	background.position.x -= 0.15
	background.position.y -= 0.3
	
	if background.position.x <= bg_start_pos.x - 80:
		background.position = bg_start_pos

func _on_submit():
	var total := 0

	for card_name in Fields:
		var text = Fields[card_name].text.strip_edges()

		if text == "":
			Result.text = "No value given for %s" % card_name
			return

		if !text.is_valid_int():
			Result.text = "'%s' is not a valid number" % text
			return

		var amount = int(text)

		if amount < 0:
			Result.text = "Values cannot be negative"
			return

		CusSet[card_name] = amount
		total += amount

	if total < 10:
		Result.text = "Deck contains too few cards"
		return

	if total > 60:
		Result.text = "Deck contains too many cards"
		return

	Result.text = "Settings Accepted"

	controller.custom_set = CusSet.duplicate()


func _on_reset():
	CusSet = StanSet.duplicate()
	
	for card_name in StanSet:
		Fields[card_name].text = str(StanSet[card_name])
	
	Result.text = ""


func _on_back():
	change_vis.emit()
