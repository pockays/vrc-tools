# 材质替换工具 - 使用示例

本文档提供了一个示例，帮助你了解如何测试和使用材质替换工具。

## 创建测试场景

以下是一个简单的测试场景设置步骤，你可以在Unity中创建这样的场景来验证材质替换工具的功能：

### 步骤1：创建测试对象

1. 创建一个空的父游戏对象，命名为"TestModel"
2. 在其下创建几个简单的3D对象作为子对象，例如：
   - 创建一个立方体，命名为"Body"
   - 创建一个球体，命名为"Head"
   - 创建一个胶囊体，命名为"Arm"

此时你的层级结构应该如下：
```
TestModel
├── Body
├── Head
└── Arm
```

### 步骤2：创建测试材质

1. 在项目的Assets文件夹中创建两个文件夹：
   - "Materials/RedTheme"
   - "Materials/BlueTheme"

2. 在RedTheme文件夹中创建以下材质：
   - Body_Mat - 设为红色
   - Head_Mat - 设为粉色
   - Arm_Mat - 设为棕红色

3. 在BlueTheme文件夹中创建具有相同名称的材质：
   - Body_Mat - 设为蓝色
   - Head_Mat - 设为浅蓝色
   - Arm_Mat - 设为深蓝色

4. 将RedTheme文件夹中的材质分配给相应的游戏对象。

### 步骤3：测试多材质对象

1. 创建一个圆环体，命名为"MultiMat"，添加为TestModel的子对象
2. 创建两个新材质：
   - 在RedTheme文件夹中创建Multi_Mat1和Multi_Mat2，分别设为红色和黄色
   - 在BlueTheme文件夹中创建相同名称的Multi_Mat1和Multi_Mat2，分别设为蓝色和青色
3. 将两个红色主题的材质分配给MultiMat的两个材质槽

## 使用材质替换工具

现在你已经设置好了测试场景，可以使用材质替换工具进行测试：

1. 打开材质替换工具窗口（Window > Material Replacer）
2. 在"目标对象"字段中选择"TestModel"
3. 在"材质文件夹"字段中选择"Assets/Materials/BlueTheme"文件夹
4. 点击"预览替换结果"，你应该能看到将要替换的材质列表
5. 点击"执行替换"按钮
6. 现在，所有对象应该从红色主题切换到蓝色主题

## 测试撤销功能

替换完成后，按Ctrl+Z（Windows）或Cmd+Z（Mac）撤销操作，所有材质应该恢复为原来的红色主题。

## 测试特殊情况

### 测试不同材质槽位

1. 创建一个新的游戏对象，比如一个平面，命名为"TestPlane"
2. 将其设为TestModel的子对象
3. 创建三个新材质：
   - 在RedTheme中创建Plane_Mat、Plane_Mat2、Plane_Mat3
   - 在BlueTheme中创建Plane_Mat、Plane_Mat2（注意没有创建Plane_Mat3）
4. 将三个红色主题的材质分配给TestPlane的三个材质槽
5. 测试材质替换工具，确认它只替换存在匹配材质的槽位（前两个）

### 测试非活动对象

1. 禁用TestModel的某个子对象
2. 运行材质替换，确认即使对象被禁用，其材质仍然得到替换（工具使用GetComponentsInChildren(true)来包含禁用的对象）

## 注意事项

- 确保材质名称完全匹配，替换基于材质名称而非材质本身的特性
- 替换操作使用Undo.RecordObject记录，可以撤销
- 工具按照材质槽位进行匹配和替换，保持原有的槽位顺序
