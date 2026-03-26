# Unity OpenRMF Integration Scripts

Unity (C#) scripts for multi-agent AMR (Autonomous Mobile Robot) simulation integrated with **ROS 2** and **Open-RMF**.

## Overview

This project provides a Unity-based simulation environment where multiple robots are controlled via ROS 2 navigation stack (Nav2) and coordinated through Open-RMF fleet management. It also includes ML-Agents reinforcement learning support for robot navigation training.

## Project Structure

```
Scripts/
├── Robot.cs                    # ML-Agents based robot agent (RL training)
├── ArrowGenerator.cs           # Visual arrow indicator generator
├── Camera Movement.cs          # Camera control
├── GUI_Robot.cs                # Robot GUI overlay
├── LocalPlayerEnabler.cs       # Local player setup
│
├── ROS/                        # ROS 2 communication layer
│   ├── AMRController.cs        # AMR controller subscribing to Nav2 cmd_vel
│   ├── AMRController_legacy.cs # Legacy AMR controller
│   ├── TinyRobotRMFController.cs # Open-RMF fleet robot controller
│   ├── CameraMovements.cs      # ROS-synced camera
│   ├── Clock.cs                # ROS clock publisher
│   ├── LiDARSensor.cs          # LiDAR sensor ROS bridge
│   └── RmfFleetMsgs/           # Custom RMF message definitions
│       ├── LocationMsg.cs
│       ├── PathRequestMsg.cs
│       ├── RobotModeMsg.cs
│       └── RobotStateMsg.cs
│
├── controller/                 # Motion control algorithms
│   ├── Controller.cs           # Controller manager (PID / Pure Pursuit)
│   ├── PID.cs                  # PID controller
│   ├── PurePursuit.cs          # Pure Pursuit path tracking
│   ├── util.cs                 # Math utilities
│   └── control.txt             # Controller parameters
│
├── lidar/                      # LiDAR simulation
│   ├── Scanner.cs              # Multi-channel LiDAR scanner
│   ├── LaserLine.cs            # Single laser ray
│   ├── PointCloud.cs           # Point cloud visualization
│   └── ScannerData.cs          # Scan data storage
│
└── Editor/                     # Unity Editor extensions
    ├── BuildingYamlGenerator.cs        # RMF building.yaml generator
    └── FleetAdapterConfigGenerator.cs  # Fleet adapter config generator
```

## Key Features

- **ROS 2 Integration**: Communicates with ROS 2 via [Unity Robotics Hub (ROS-TCP-Connector)](https://github.com/Unity-Technologies/ROS-TCP-Connector), publishing odometry/TF/clock and subscribing to `cmd_vel`.
- **Open-RMF Fleet Management**: `TinyRobotRMFController` publishes `robot_state` and follows `robot_path_requests` from the RMF fleet manager, with coordinate calibration support.
- **ML-Agents RL Training**: `Robot.cs` extends ML-Agents `Agent` for reinforcement learning-based navigation with velocity/acceleration control modes.
- **Motion Controllers**: PID and Pure Pursuit controllers with configurable parameters.
- **LiDAR Simulation**: Configurable multi-channel LiDAR scanner with point cloud output.
- **Editor Tools**: Automatic generation of RMF-compatible building YAML and fleet adapter configuration files.

## Dependencies

- [Unity 2022.3+](https://unity.com/)
- [Unity Robotics Hub (ROS-TCP-Connector)](https://github.com/Unity-Technologies/ROS-TCP-Connector)
- [Unity ML-Agents Toolkit](https://github.com/Unity-Technologies/ml-agents)
- [ROS 2 (Humble+)](https://docs.ros.org/)
- [Open-RMF](https://github.com/open-rmf)

## License

This project is provided for academic and research purposes.
