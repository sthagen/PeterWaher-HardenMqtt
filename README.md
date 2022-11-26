Harden MQTT
================

This project is aimed at professors teaching and students learning IoT communication over MQTT, and how to harden their 
implementations so they avoid most common mistakes and vulnerabilities when using MQTT.

Projects
---------

This repository consists of the following projects. They are developed in C#.

| Project            | Description |
|:-------------------|:------------|
| [Sensor](Sensor)   | This project implements a simple sensor that gets its values from the Internet and publishes it on an MQTT Broker. It publishes the information in five different ways: Unstructured, Structured, Interoperable (first three unsecured), and public, Confidential (last two secured). |
| [Display](Display) | This project presents sensor information published by the Sensor project. It displays information generated by all five methods transmitted. |
| [Troll](Troll)     | This project tries to spy on, prohibit or alter information sent by sensors connected on the MQTT broker. |

For the Teacher
-----------------

You can find a proposed [Lab](LAB.md) in this repository, divided into three sessions, where studens learn to communicate over
MQTT, then learn about common vulnerabilities, and finally how to harden their solutions for these vulnerabilities. The projects
available in this repository can be run simultaneously during the lab, everyone using the same MQTT broker as shared infrastructure.

Other repositories of interest
----------------------------------

| Repository                                                                 | Description |
|:---------------------------------------------------------------------------|:------------|
| [Mastering Internet of Things](https://github.com/PeterWaher/MIoT)         | Contains multiple projects for sensors, actuators and concentrators using different protocols for comparison (such as HTTP, CoAP, LWM2M, MQTT and XMPP). All projects and associated information are described in a book with the same name. |
| [OpenWeatherMapSensor](https://github.com/PeterWaher/OpenWeatherMapSensor) | Contains a simple provisionable sensor publishing sensor data using the XMPP protocol. |
| [IoTGateway](https://github.com/PeterWaher/IoTGateway)                     | Contains multiple libraries for creating distributed applications for IoT, as well as an IoT Gateway host and client applications. Apart from containing the source code of the libraries used by the projects in this repository, it also contains a useful tool for [viewing events](https://github.com/PeterWaher/IoTGateway/tree/master/Clients/Waher.Client.MqttEventViewer) in distributed applications over MQTT, something that will come in handy when debugging or tracing what happens during development or the course of the lab. |
| [TAG Digital ID](https://github.com/Trust-Anchor-Group/IdApp)              | A Digital ID App that can be used for communicating with IoT devices, as well as to provision access to them, among many other things. |
| [TAG ComSim](https://github.com/Trust-Anchor-Group/ComSim)                 | A communications-based simulator, which allows you to create stochastic simulation models to simulate large networks of IoT devices, as well as human users. |
| [TAG LegalLab](https://github.com/Trust-Anchor-Group/LegalLab)             | A tool for creating smart contracts that can be used for automation across domains in IoT and smart societies. |
| [IEEE XMPP IoT Interfaces](https://gitlab.com/IEEE-SA/XMPPI/IoT)           | A repository hosted by IEEE for IoT Harmonization for smart societies. Contains the technical reference information necessary to understand cross-domain interoperability, digital identities, smart contracts, decision support and automated, legally binding, decision making, as the basis for automation across domains on the Internet. |
