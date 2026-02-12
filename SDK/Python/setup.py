"""星尘 Python SDK 安装配置"""

from setuptools import setup, find_packages

with open("README.md", "r", encoding="utf-8") as fh:
    long_description = fh.read()

setup(
    name="stardust-sdk",
    version="1.0.0",
    author="NewLife Team",
    author_email="support@newlifex.com",
    description="星尘监控 Python SDK，提供 APM 监控和配置中心功能",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/NewLifeX/Stardust",
    packages=find_packages(),
    classifiers=[
        "Development Status :: 4 - Beta",
        "Intended Audience :: Developers",
        "Topic :: Software Development :: Libraries :: Python Modules",
        "Topic :: System :: Monitoring",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
    ],
    python_requires=">=3.7",
    install_requires=[
        "requests>=2.25.0",
    ],
    keywords="stardust apm monitoring config tracing",
    project_urls={
        "Bug Reports": "https://github.com/NewLifeX/Stardust/issues",
        "Source": "https://github.com/NewLifeX/Stardust",
        "Documentation": "https://newlifex.com/blood/stardust_monitor",
    },
)
