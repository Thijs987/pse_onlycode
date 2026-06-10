extends Node

signal progress_changed(progress)
signal loading_finished

var loading_screen: PackedScene = preload("uid://dmhqmq1eo7wey")
var loaded_resource: PackedScene
var scene_path: String
var progress: Array = []
var use_sub_threads: bool = true

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	set_process(false)

# Head function to call when wanting to switch between scenes.
# Calls all other necessary function to switch between scenes
# input is the uid of the scene
func load_scene(_scene_path: String) -> void:
	scene_path = _scene_path
	
	var new_loading_screen = loading_screen.instantiate()
	add_child(new_loading_screen)
	progress_changed.connect(new_loading_screen._on_progress_changed)
	loading_finished.connect(new_loading_screen._on_load_finished)
	
	await new_loading_screen.loading_screen_ready
	
	start_load()

# Sets _process to true to start loading of next scene
func start_load() -> void:
	var state = ResourceLoader.load_threaded_request(scene_path, "", use_sub_threads)
	
	if state == OK:
		set_process(true)

# Loads the next scene when _process is set to true.
func _process(_delta: float) -> void:
	var load_status = ResourceLoader.load_threaded_get_status(scene_path, progress)
	progress_changed.emit(progress[0])
	match load_status:
		ResourceLoader.THREAD_LOAD_INVALID_RESOURCE, ResourceLoader.THREAD_LOAD_FAILED:
			set_process(false)
		ResourceLoader.THREAD_LOAD_LOADED:
			loaded_resource = ResourceLoader.load_threaded_get(scene_path)
			get_tree().change_scene_to_packed(loaded_resource)
			loading_finished.emit()
	
