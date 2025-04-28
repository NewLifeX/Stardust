using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeployAgent.Commands;

internal interface ICommand
{
    void Process(String[] args);
}
