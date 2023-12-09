import os
import sys
import time

import PIL.Image
import socket

from ai2thor.controller import Controller
from pynput import keyboard


############## Socket ##############
def send_files_to_tapa(client_socket):
    directions = ["left", "front", "right"]
    file_paths = ['../inputs/LAVIS/caption', '../output/objects/objects']
    extensions = ['.txt', '.txt']
    for file_path, extension in zip(file_paths, extensions):
        for direction in directions:
            file_name = f'{frame_counter:04}_{direction}{extension}'
            file_path_complete = file_path + file_name
            file_name = file_path_complete.rsplit('/', 1)[-1]
            # Send the filename first
            client_socket.send(file_name.encode("utf-8"))

            # Send a delimiter to separate filename and content
            client_socket.send(b'\0')

            with open(file_path_complete, 'rb') as text_file:
                # Send file in batches
                text_data = text_file.read(1024)
                while text_data:
                    client_socket.send(text_data)
                    text_data = text_file.read(1024)

            # Send a marker to indicate the end of the file
            client_socket.send(b'FILE_END')
            print(f"Frame{frame_counter:04}_{direction}{extension} sent via socket.")
            time.sleep(0.5)


def connect_to_server(port, host='192.168.1.204'):
    while True:
        try:
            client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            client_socket.connect((host, port))
            print("Connected to server.")
            return client_socket
        except ConnectionRefusedError:
            print("Connection refused. Retrying in 5 seconds...")
            time.sleep(5)  # Wait for 5 seconds before retrying


def send_files_to_lavis(client_socket):
    directions = ["left", "front", "right"]
    file_paths = ['../output/frames/']
    extensions = ['.jpg']
    for file_path, extension in zip(file_paths, extensions):
        for direction in directions:
            file_name = f'frame{frame_counter:04}_{direction}{extension}'
            file_path_complete = file_path + file_name
            if "objects" in file_path_complete:
                send_txt_file(file_path_complete, file_name, client_socket)
            elif "frames" in file_path:
                send_jpg_file(file_path_complete, file_name, client_socket)

            # Send a marker to indicate the end of the file
            client_socket.send(b'FILE_END')
            print(f"{frame_counter:04}_{direction}{extension} sent via socket.")
            time.sleep(0.5)


def send_jpg_file(file, file_name, client_socket):
    # Send the filename first
    client_socket.send(file_name.encode("utf-8"))

    # Send a delimiter to separate filename and content
    client_socket.send(b'\0')

    with open(file, 'rb') as image_file:
        image_data = image_file.read(1024)
        while image_data:
            client_socket.send(image_data)
            image_data = image_file.read(1024)


def send_txt_file(file, file_name, client_socket):
    # Send the filename first
    client_socket.send(file_name.encode("utf-8"))

    # Send a delimiter to separate filename and content
    client_socket.send(b'\0')
    with open(file, 'rb') as text_file:
        # Send file in batches
        text_data = text_file.read(1024)
        while text_data:
            client_socket.send(text_data)
            text_data = text_file.read(1024)


def receive_data_from_socket(server, client_socket):
    while True:
        file_data = b''
        file_name = b''
        save_location = ""

        # Receive the filename
        while True:
            data = client_socket.recv(1)
            if data == b'\0':
                break
            file_name += data
        # Decode the filename
        file_name = file_name.decode("utf-8")

        # Specify the saving location and file name
        if server == "tapa":
            save_location = "../inputs/tapa"
        elif server == "LAVIS":
            save_location = "../inputs/LAVIS"
        file_path = os.path.join(save_location, file_name)

        # Receive and save the file based on its extension
        with open(file_path, 'wb') as file:
            buffer = b''
            while True:
                # Receive and save the text file
                text_data = client_socket.recv(1024)
                if not text_data:
                    break
                # Add the newly received data to the buffer
                buffer += text_data
                while b'FILE_END' in buffer:
                    # Split buffer at the first occurrence of 'FILE_END'
                    file_data, buffer = buffer.split(b'FILE_END', 1)
                    # Write the data to the file
                    # file_data = file_data.decode("utf-8")
                    file.write(file_data)
                    print(f"File {file_name} from Socket saved.")
                    return

def disconnect_from_server(client_socket):
    client_socket.close()


############## Keyboard ##############
def wait_and_return_key():
    key_pressed = None

    def on_key_press(key):
        nonlocal key_pressed
        key_pressed = key
        return False  # Stop the listener once a key is pressed

    with keyboard.Listener(on_press=on_key_press) as listener:
        listener.join()

    if key_pressed is not None:
        try:
            return key_pressed.char  # Convert the key to a string
        except AttributeError:
            return str(key_pressed)  # Handle special keys as strings
    else:
        return None  # No key was pressed


# Define the key_to_action dictionary
key_to_action = {
    "Key.up": "LookUp",
    "Key.down": "LookDown",
    "Key.left": "RotateLeft",
    "Key.right": "RotateRight",
    "Key.esc": "esc",
    'a': "MoveLeft",
    'd': "MoveRight",
    's': "MoveBack",
    'w': "MoveAhead"
}

############## Controller ##############

# Define and initialize the controller variable at the module level
controller = Controller(scene="FloorPlan10")


def controller_single_input():
    while True:
        key = wait_and_return_key()
        action = key_to_action.get(key)
        if action is None:
            print("Use valid keys.")
        elif action == "esc":
            return None, "Key.esc"
        else:
            break
    event = controller.step(action=action)
    return event, key


############## Vision ##############
frame_counter = 0


def save_frame(frame, direction):
    # Read image
    # Check if the image was successfully loaded
    if frame is not None:
        # Create an Image object from the ndarray
        image = PIL.Image.fromarray(frame)
        # Specify the relative path to the parent directory and format
        relative_path = "../output/frames/frame" + f'{frame_counter:04}_{direction}' + ".jpg"
        # Get the current working directory
        current_directory = os.getcwd()
        # Construct the full output file path
        output_file = os.path.join(current_directory, relative_path)
        # Save the image to the specified file
        image.save(output_file, 'JPEG')
        print(f"Image saved as {output_file}")
    else:
        print("Failed to load the image")


def save_surrounding():
    rotation = ["RotateLeft", "RotateRight", "RotateLeft"]
    views = ["left", "right", "front"]
    degrees = [80, 160, 80]
    for rot, deg, view in zip(rotation, degrees, views):
        event = controller.step(rot, degrees=deg)
        save_frame(event.frame, view)
        objects = detect_objects()
        save_objects(objects, view)

    controller.step(action="Done")


def detect_objects():
    objects = controller.last_event.metadata["objects"]
    visible_objects = []

    for obj in objects:
        if obj["visible"] is True:
            visible_objects.append(obj["name"])

    already_seen_objects = {}
    # Initialize a new list to store unique objects
    unique_visible_objects = []

    for obj in visible_objects:
        # Extract the base name (without the random number) from the object
        base_name = obj.split('_')[0]

        # Check if we've seen this base name before
        if base_name not in already_seen_objects:
            already_seen_objects[base_name] = True  # Mark it as seen
            unique_visible_objects.append(base_name)  # Append it to the unique_objects list

    return unique_visible_objects


def save_objects(objects, direction):
    # Specify the relative path to the parent directory and format
    relative_path = "../output/objects/objects" + f'{frame_counter:04}_{direction}' + ".txt"
    # Get the current working directory
    current_directory = os.getcwd()
    # Construct the full output file path
    output_file = os.path.join(current_directory, relative_path)
    # Open the file in write mode (w)
    with open(output_file, "w") as file:
        for obj in objects:
            file.write(obj + "\n")  # Write each item to the file followed by a newline character


def main():
    global frame_counter

    client_socket_lavis = connect_to_server(54321)
    client_socket_tapa = connect_to_server(54320)

    send_files_to_tapa(client_socket_tapa)
    receive_data_from_socket("tapa", client_socket_tapa)

    controller.step(action="Done")

    while True:
        save_surrounding()
        send_files_to_lavis(client_socket_lavis)
        for i in range(3):
            receive_data_from_socket("lavis", client_socket_lavis)

        event, key = controller_single_input()
        if key == "Key.esc":
            break

        frame_counter += 1

    # disconnect_from_server(client_socket_lavis)
    # display_frame(event.frame)
    # LLM.get_instruction_from_llm()


if __name__ == "__main__":
    main()

    sys.exit()
