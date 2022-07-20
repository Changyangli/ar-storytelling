# Interactive Augmented Reality Storytelling Guided by Scene Semantics

<div align="center">
    <img src="assets/teaser.jpg", width="900">
</div>
<br>
This repository is the implementation for our SIGGRAPH 2022 paper: "Interactive Augmented Reality Storytelling Guided by Scene Semantics"

[[Paper](https://changyangli.github.io/assets/paper/sig22arstorytelling.pdf)] [[Video](https://www.youtube.com/watch?v=LGzH2LikEUw&feature=youtu.be)] [[Project page](https://changyangli.github.io/projects/siggraph22arstorytelling/)] <br>
[Changyang Li](https://changyangli.github.io/), [Wanwan Li](), [Haikun Huang](https://quincyhuang.github.io/Webpage/), [Lap-Fai Yu](https://craigyuyu.github.io/home/)


-----------

## Introduction

We present a novel interactive augmented reality (AR) storytelling approach guided by indoor scene semantics. Our approach automatically populates virtual contents in real-world environments to deliver AR stories, which match both the story plots and scene semantics. During the storytelling process, a player can participate as a character in the story. Meanwhile, the behaviors of the virtual characters and the placement of the virtual items adapt to the player's actions.

## Setup

- Environment: Unity3D 2020.1.16f1
- Augmented reality device: Microsoft Hololens 2
- Mixed reality toolkit: [MRTK](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/mrtk2/) 2.7.2.0

To deliver full AR experiences, our framework relies on some commercial Unity3D assets for virtual models, animations, navigation, etc. For example:
- [Low Poly Animated People](https://assetstore.unity.com/packages/3d/characters/humanoids/low-poly-animated-people-156748)
- [POLYGON City](https://assetstore.unity.com/packages/3d/environments/urban/polygon-city-low-poly-3d-art-by-synty-95214)
- [Final IK](https://assetstore.unity.com/packages/tools/animation/final-ik-14290)
- [A* Pathfinding Project Pro](https://assetstore.unity.com/packages/tools/ai/a-pathfinding-project-pro-87744#description)
- [UMotion Pro - Animation Editor](https://assetstore.unity.com/packages/tools/animation/umotion-pro-animation-editor-95991)

## Citation

    @article{arstorytelling,
        title={Interactive Augmented Reality Storytelling Guided by Scene Semantics},
        author = {Changyang Li and Wanwan Li and Haikun Huang and Lap-Fai Yu},
        journal = {ACM Transactions on Graphics (TOG)},
        volume = {41},
        number = {4},
        year = {2022},
        publisher={ACM New York, NY, USA}
    }


