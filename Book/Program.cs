using BenchmarkDotNet.Running;
using BookCollection;


//BenchmarkRunner.Run<MonitorTest>();

await new Test().TestBookCollection();
