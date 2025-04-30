using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeployAgent.Commands;

internal class DeployCommand : ICommand
{
    public void Process(String[] args)
    {

    }
}

class DeployParameter
{
    public String AppId { get; set; }
}